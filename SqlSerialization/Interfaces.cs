using System;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace SqlSerialization
{
    public interface IColumn
    {
        string Name { get; set; }
        SqlDbType DbType { get; set; }
        bool PrimaryKey { get; set; }
        string propertyName { get; set; }
        Type propertyType { get; set; }
        bool ForeignKey { get; set; }
    }

    public interface ITable
    {
        Type Type { get; set; }
        IColumn PrimaryKey { get; }
        List<IColumn> Columns { get; set; }
        List<ITable> ForeignTables { get; set; }
        string Name { get; set; }
        List<Dictionary<string, object>> Rows { get; set; }
        void Add(object objData);
    }

    public interface IDatabase
    {
        List<ITable> Tables { get; set; }
        void Add(object objData);
    }
}
