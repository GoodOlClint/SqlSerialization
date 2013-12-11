using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;

namespace SqlSerialization.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            SqlSerializer serializer = new SqlSerializer(new SQLiteFactory());
            Test t = new Test();
            t.ID = 5;
            t.Name = "test Class";
            t.objComplex = new ComplexObject();

            t.objComplex.Name = "test ComplexObject";
            
            serializer.Serialize(t, "Data Source=cache.db3");
            Test tt = (Test)serializer.Deserialize("Data Source=cache.db3");
            
            Console.Write(t.Equals(tt));
            Console.Read();
        }
    }

    [Serializable]
    public class Test
    {
        public string Name { get; set; }
        public int ID;
        public ComplexObject objComplex { get; set; }
    }

    public class ComplexObject
    {
        public string Name { get; set; }
    }
}
