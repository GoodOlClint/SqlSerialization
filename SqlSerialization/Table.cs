using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data.Common;

namespace SqlSerialization
{
    public sealed class Table<T> : ITable where T:new()
    {
        public IColumn PrimaryKey
        {
            get
            {
                return (from c in this.Columns
                        where c.PrimaryKey == true
                        select c).FirstOrDefault();
            }
        }

        private readonly object rowLock = new object();

        public List<IColumn> Columns { get; set; }
        public List<Dictionary<string, object>> Rows { get; set; }
        public List<ITable> ForeignTables { get; set; }
        public string Name { get; set; }

        public Table()
        {
            this.Columns = new List<IColumn>();
            this.Rows = new List<Dictionary<string, object>>();
            this.ForeignTables = new List<ITable>();
            ((ITable)this).Type = typeof(T);

            if (Attribute.IsDefined(typeof(T), typeof(SqlTableAttribute)))
            { this.Name = ((SqlTableAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(SqlTableAttribute))).Name; }
            else
            { this.Name = typeof(T).Name; }


            var members = (from t in typeof(T).GetProperties()
                           where !Attribute.IsDefined(t, typeof(SqlIgnoreAttribute))
                           select new
                           {
                               Name = t.Name,
                               PropertyType = t.PropertyType
                           }).ToList();

            members.AddRange((from t in typeof(T).GetFields()
                              where !Attribute.IsDefined(t, typeof(SqlIgnoreAttribute))
                              select new
                              {
                                  Name = t.Name,
                                  PropertyType = t.FieldType
                              }));

            bool hasSubTypes = false;
            foreach (var MI in members)
            {
                IColumn column;

                if (Attribute.IsDefined(MI.PropertyType, typeof(SqlDataAttribute)))
                { column = (IColumn)Attribute.GetCustomAttribute(typeof(T), typeof(SqlDataAttribute)); }
                else
                { column = new SqlDataAttribute(); }

                column.propertyType = MI.PropertyType;
                column.propertyName = MI.Name;

                if ((!MI.PropertyType.IsValueType) && (MI.PropertyType != typeof(string)))
                {
                    hasSubTypes = true;
                    column.Name = "fk_" + MI.PropertyType.Name;
                    column.DbType = System.Data.SqlDbType.Int;
                    column.ForeignKey = true;
                    Type thisType = typeof(Table<>);
                    Type newType = thisType.MakeGenericType(column.propertyType);
                    this.ForeignTables.Add((ITable)Activator.CreateInstance(newType));
                }
                if (string.IsNullOrEmpty(column.Name))
                { column.Name = column.propertyName; }
                this.Columns.Add(column);
            }
            var hasKey = (from c in this.Columns
                          where c.PrimaryKey == true
                          select c).Count() > 0;

            if (hasSubTypes && !hasKey)
            {
                IColumn key = new SqlDataAttribute();
                key.DbType = System.Data.SqlDbType.Int;
                key.Name = "pk_" + this.Name;
                key.PrimaryKey = true;
                this.Columns.Insert(0, key);
            }
        }

        public void Add(object objData)
        {

        }

        internal Table(IEnumerable<object> listData)
            : this()
        {
            //this.Rows.AddRange(listData);
        }

        Type ITable.Type { get; set; }

        

        //public void Save(ICollection<T> Data, DbConnection dbConn)
        //{
        //    using (DbCommand dbCmd = dbConn.CreateCommand())
        //    {
        //        dbCmd.CommandText = SQLCreateStatement();
        //        dbCmd.ExecuteNonQuery();
        //        dbCmd.CommandText = SQLInsertStatement();
        //        Dictionary<string, DbParameter> dbParams = new Dictionary<string, DbParameter>();

        //        foreach (string ColumnName in from column in this.Columns
        //                                      select column.propertyName)
        //        {
        //            DbParameter dbParam = dbCmd.CreateParameter();
        //            dbParam.ParameterName = "@" + ColumnName;
        //            dbParams.Add(ColumnName, dbParam);
        //        }
        //        using (DbTransaction dbTrans = dbConn.BeginTransaction())
        //        {
        //            try
        //            {
        //                foreach (T data in Data)
        //                {
        //                    dbCmd.Parameters.Clear();
        //                    foreach (var c in this.Columns)
        //                    { dbParams[c.Name].Value = GetMemberValue(data, c.propertyName); }
        //                    dbCmd.Parameters.AddRange(dbParams.Values.ToArray());
        //                    dbCmd.ExecuteNonQuery();
        //                }
        //                dbTrans.Commit();
        //            }
        //            catch
        //            {
        //                dbTrans.Rollback();
        //            }
        //        }
        //    }
        //}

        //public void Save(T Data, DbConnection dbConn)
        //{
        //    List<T> newData = new List<T>();
        //    newData.Add(Data);
        //    this.Save(newData, dbConn);
        //}

        //public List<T> Load(DbConnection dbConn)
        //{
        //    List<T> dataList = new List<T>();

        //    using (DbCommand dbCmd = dbConn.CreateCommand())
        //    {
        //        dbCmd.CommandText = this.SQLSelectStatement();
        //        using (DbDataReader reader = dbCmd.ExecuteReader())
        //        {
        //            bool nextResult = true;
        //            while (nextResult)
        //            {
        //                while (reader.Read())
        //                {
        //                    T data = new T();
        //                    //PropertyInfo[] Prop = typeof(TData).GetProperties();
        //                    foreach (IColumn column in this.Columns)
        //                    {
        //                        PropertyInfo PI = (from PropertyInfo Prop in typeof(T).GetProperties()
        //                                           where Prop.Name == column.propertyName
        //                                           select Prop).First();
        //                        object newData = Convert.ChangeType(reader[column.Name], PI.PropertyType);
        //                        PI.SetValue(data, newData, null);
        //                    }
        //                    dataList.Add(data);
        //                }
        //                nextResult = reader.NextResult();
        //            }
        //        }

        //    }

        //    return dataList;
        //}

        internal object GetMemberValue(object objData, string memberName)
        {
            object value = null;
            var values = (from member in objData.GetType().GetMembers()
                          where member.Name == memberName
                          select member);

            foreach (var v in values)
            {
                switch (v.MemberType)
                {
                    case MemberTypes.Property:
                        value = ((PropertyInfo)v).GetValue(objData, null);
                        break;
                    case MemberTypes.Field:
                        value = ((FieldInfo)v).GetValue(objData);
                        break;
                }
            }
            return value;
        }
    }
}
