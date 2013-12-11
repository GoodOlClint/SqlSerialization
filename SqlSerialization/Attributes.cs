using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace SqlSerialization
{
    public class SqlDataAttribute : Attribute, IColumn
    {
        public string Name { get; set; }
        public SqlDbType DbType { get; set; }
        bool IColumn.PrimaryKey { get; set; }
        string IColumn.propertyName { get; set; }
        Type IColumn.propertyType { get; set; }
        bool IColumn.ForeignKey { get; set; }

        public SqlDataAttribute()
        { this.DbType = SqlDbType.NVarChar; }
    }

    public class SqlTableAttribute : Attribute
    {
        public string Name { get; set; }
    }

    public class SqlIgnoreAttribute : Attribute
    {
    }
}
