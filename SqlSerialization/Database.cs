using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data.Common;

namespace SqlSerialization
{
    public class Database : IDatabase
    {
        private readonly object dbLock = new object();

        public List<ITable> Tables { get; set; }

        List<ITable> linkedTables { get; set; }
        DbProviderFactory factory;

        public Database(DbProviderFactory dbFactory)
        {
            this.Tables = new List<ITable>();
            this.factory = dbFactory;
        }

        public void Initalize()
        {
            List<ITable> results = new List<ITable>();
            foreach (ITable table in this.Tables)
            {
                results.AddRange(this.GetAllTables(table));
            }
            this.Tables = results.Distinct().ToList();
        }

        public List<ITable> GetAllTables(ITable Table)
        {
            List<ITable> results = new List<ITable>();
            foreach (ITable table in Table.ForeignTables)
            {
                if (table.PrimaryKey == null)
                {
                    IColumn key = new SqlDataAttribute();
                    key.DbType = System.Data.SqlDbType.Int;
                    key.Name = "pk_" + table.Name;
                    key.PrimaryKey = true;
                    table.Columns.Insert(0, key);
                }
                results.AddRange(this.GetAllTables(table));
            }
            results.Add(Table);
            return results;
        }

        public void Add(object objData)
        {
            Dictionary<string, object> row = new Dictionary<string, object>();
            //lock (dbLock)
            //{
            ITable table = (from T in this.Tables
                            where T.Type == objData.GetType()
                            select T).First();

            foreach (IColumn c in table.Columns)
            {
                object value = null;

                if (c.PrimaryKey)
                { value = table.Rows.Count + 1; }
                else if (c.ForeignKey)
                {
                    ITable foreignTable = (from T in this.Tables
                                           where T.Type == c.propertyType
                                           select T).First();
                    Dictionary<string, object> data = new Dictionary<string, object>();

                    object propData = GetMemberValue(objData, c.propertyName);
                    //data.Add(foreignTable.PrimaryKey.Name, propData);
                    try { GetRowID(foreignTable, propData); }
                    catch { Add(propData); }

                    //foreignTable.Rows.Contains(
                    int ID = GetRowID(foreignTable, propData);

                    value = ID;

                }
                else
                {
                    value = GetMemberValue(objData, c.propertyName);
                }
                row.Add(c.Name, value);
            }
            table.Rows.Add(row);
            //}
        }

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

        internal int GetRowID(ITable table, object objData)
        {
            IQueryable<Dictionary<string, object>> query = table.Rows.AsQueryable();
            foreach (IColumn column in table.Columns)
            {
                string name = column.propertyName;
                object data = GetMemberValue(objData, name);
                if (!string.IsNullOrEmpty(name))
                { query = query.Where(p => p[name] == data); }
            }
            return Convert.ToInt32(query.First()[table.PrimaryKey.Name]);

        }


        public void Save(string connectionString)
        {
            using (DbConnection dbConn = this.factory.CreateConnection())
            {
                dbConn.ConnectionString = connectionString;
                dbConn.Open();
                using (DbCommand dbCmd = dbConn.CreateCommand())
                {
                    foreach (ITable T in this.Tables)
                    {
                        dbCmd.CommandText = SQLCreateStatement(T);
                        dbCmd.ExecuteNonQuery();
                        dbCmd.CommandText = SQLInsertStatement(T);
                        Dictionary<string, DbParameter> dbParams = new Dictionary<string, DbParameter>();

                        foreach (string ColumnName in from column in T.Columns
                                                      select column.Name)
                        {
                            DbParameter dbParam = dbCmd.CreateParameter();
                            dbParam.ParameterName = "@" + ColumnName;
                            dbParams.Add(ColumnName, dbParam);
                        }
                        using (DbTransaction dbTrans = dbConn.BeginTransaction())
                        {
                            try
                            {
                                foreach (var row in T.Rows)
                                {
                                    dbCmd.Parameters.Clear();
                                    foreach (var c in T.Columns)
                                    { dbParams[c.Name].Value = row[c.Name]; }
                                    dbCmd.Parameters.AddRange(dbParams.Values.ToArray());
                                    dbCmd.ExecuteNonQuery();
                                }
                                dbTrans.Commit();
                            }
                            catch
                            {
                                dbTrans.Rollback();
                            }
                        }
                    }
                }
            }
        }

        public void Load(string connectionString)
        {
            using (DbConnection dbConn = this.factory.CreateConnection())
            {
                dbConn.ConnectionString = connectionString;
                dbConn.Open();
                foreach (ITable T in this.Tables)
                {
                    using (DbCommand dbCmd = dbConn.CreateCommand())
                    {
                        dbCmd.CommandText = SQLSelectStatement(T);
                        using (DbDataReader reader = dbCmd.ExecuteReader())
                        {
                            bool nextResult = true;
                            while (nextResult)
                            {
                                while (reader.Read())
                                {
                                    foreach (IColumn column in T.Columns)
                                    {
                                        Dictionary<string, object> Row = new Dictionary<string, object>();
                                        Row[column.Name] = reader[column.Name];
                                    }
                                }
                                nextResult = reader.NextResult();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds a query string that can be run against a SQL database to select all data for the current table
        /// </summary>
        /// <returns>a string containing the SQL Select statement for the current table</returns>
        public string SQLSelectStatement(ITable T)
        {
            return string.Format("SELECT * FROM {0}", T.Name);
        }

        /// <summary>
        /// Builds a query string that can be run against a SQL database to create the database structure for the current table
        /// </summary>
        /// <returns>a string containing the SQL Create statement for the current table, including all indexes</returns>
        public string SQLCreateStatement(ITable T)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.AppendFormat("CREATE TABLE IF NOT EXISTS {0} (\r\n\t", T.Name);

            string[] columns = new string[T.Columns.Count + 1];
            int i = 0;
            List<string> primaryKey = new List<string>();

            /* We're going to loop through all the columns and append the column name to the sql string */
            foreach (IColumn Column in T.Columns)
            {
                string strSql = Column.Name;

                strSql += string.Format(" {0}", Column.DbType.ToString());

                if (Column.PrimaryKey)
                { primaryKey.Add(Column.Name); }

                columns[i] = strSql;
                i = i + 1;
            }
            /* build primary key */
            if (primaryKey.Count > 0)
            {
                columns[i] = string.Format("PRIMARY KEY ({0})", string.Join(", ", primaryKey.ToArray()));
            }
            /* join the columns together separated by a a comma, a new line, and a tab */
            sbSql.AppendLine(string.Join(",\r\n\t", columns.Where(s => !string.IsNullOrEmpty(s))));

            /* close the statement and add a new line*/
            sbSql.AppendLine(");");

            /* Check to see if we have any indexes for this table */
            //if (this.Indexes.Count > 0)
            //{
            //    /* If we do loop through them all*/
            //    foreach (KeyValuePair<string, List<string>> index in this.Indexes)
            //    {
            //        sbSql.AppendFormat("CREATE INDEX IF NOT EXISTS IDX_{0} ON {1} (", index.Key, TableName);
            //        sbSql.Append(string.Join(", ", index.Value.ToArray()));
            //        sbSql.AppendLine(");");
            //    }
            //}
            /* Finally, return our SQL statement */
            return sbSql.ToString();
        }

        /// <summary>
        /// Builds a query string that can be run against a SQL database to create the database structure for the current table
        /// </summary>
        /// <returns>a string containing the SQL Create statement for the current table, including all indexes</returns>
        public string SQLInsertStatement(ITable T)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.AppendFormat("INSERT INTO {0} (\r\n\t", T.Name);

            string[] columns = (from column in T.Columns
                                select column.Name).ToArray();

            string[] values = (from column in T.Columns
                               select string.Format("@{0}", column.Name)).ToArray();

            sbSql.AppendLine(string.Join(",\r\n\t", columns));
            sbSql.Append(") VALUES (\r\n\t");

            sbSql.AppendLine(string.Join(",\r\n\t", values));
            sbSql.Append(");");

            /* Finally, return our SQL statement */
            return sbSql.ToString();
        }


    }
}