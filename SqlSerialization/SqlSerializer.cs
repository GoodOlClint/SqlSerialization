using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Sql;
using System.Runtime.Serialization;
using System.Reflection;
using System.Linq;
using System.Data.Common;

namespace SqlSerialization
{
    public class SqlSerializer
    {
        Database database;

        public SqlSerializer(Type type, DbProviderFactory factory)
        {
            this.database = new Database(factory);
            Type thisType = typeof(Table<>);
            Type newType = thisType.MakeGenericType(type);

            this.database.Tables.Add((ITable)Activator.CreateInstance(newType));

            foreach (Type t in (from MemberInfo member in type.GetMembers()
                                where Attribute.IsDefined(member.GetType(), typeof(SerializableAttribute))
                                select member.GetType()))
            {
                newType = thisType.MakeGenericType(t);
                this.database.Tables.Add((ITable)Activator.CreateInstance(newType));
            }
            this.database.Initalize();
        }

        public SqlSerializer(DbProviderFactory factory)
        {
            this.database = new Database(factory);
            Type[] types = Assembly.GetCallingAssembly().GetTypes();

            foreach (Type t in types)
            {
                if (Attribute.IsDefined(t, typeof(SerializableAttribute)))
                {
                    Type thisType = typeof(Table<>);
                    Type newType = thisType.MakeGenericType(t);
                    this.database.Tables.Add((ITable)Activator.CreateInstance(newType));
                }
            }
            this.database.Initalize();
        }

        public void Serialize(object objData, string connectionString)
        {
            this.database.Add(objData);
            this.database.Save(connectionString);
        }

        public object Deserialize(string connectionString)
        {
            this.database.Load(connectionString);
            List<object> returnData = new List<object>();
            List<ITable> skipTables = new List<ITable>();
            foreach (ITable T in this.database.Tables.OrderByDescending(T => T.ForeignTables.Count))
            {
                if (skipTables.Contains(T))
                { continue; }
                skipTables.AddRange(T.ForeignTables);
                foreach (var row in T.Rows)
                { returnData.Add(convertData(T.Type, row)); }
            }
            if (returnData.Count > 1)
            { return returnData; }
            else
            { return returnData.FirstOrDefault(); }

        }

        protected internal object convertData(Type dataType, Dictionary<string, object> dataRow)
        {
            ITable Table = (from T in this.database.Tables
                            where T.Type == dataType
                            select T).First();
            object data = Activator.CreateInstance(dataType);
            foreach (IColumn c in Table.Columns)
            {
                if (string.IsNullOrEmpty(c.propertyName))
                { continue; }


                MemberInfo MI = (from MemberInfo member in dataType.GetMembers()
                                 where member.Name == c.propertyName
                                 select member).First();

                if (c.ForeignKey)
                {
                    ITable foreignTable = (from T in this.database.Tables
                                           where T.Type == c.propertyType
                                           select T).First();
                    Dictionary<string, object> foreignData = (from R in foreignTable.Rows
                                                              where Convert.ToInt32(R[foreignTable.PrimaryKey.Name]) == Convert.ToInt32(dataRow[c.Name])
                                                              select R).First();

                    switch (MI.MemberType)
                    {
                        case MemberTypes.Property:
                            ((PropertyInfo)MI).SetValue(data, convertData(c.propertyType, foreignData), null);
                            break;
                        case MemberTypes.Field:
                            ((FieldInfo)MI).SetValue(data, convertData(c.propertyType, foreignData));
                            break;
                    }
                }
                else
                {
                    object value = Convert.ChangeType(dataRow[c.Name], c.propertyType);
                    switch (MI.MemberType)
                    {
                        case MemberTypes.Property:
                            ((PropertyInfo)MI).SetValue(data, value, null);
                            break;
                        case MemberTypes.Field:
                            ((FieldInfo)MI).SetValue(data, value);
                            break;
                    }
                }
            }
            return data;
        }
    }
}
