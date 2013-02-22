using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GenericConnector;
using DatabaseUtilities;

namespace DatabaseUtilities
{
    public abstract class StructureUtilities
    {
        protected GenConnection Connection;

        public StructureUtilities(GenConnection dbCon)
        {
            Connection = dbCon;
        }

        public abstract SchemaStructure StructureFromDatabase(String name);
    }


    public class StructureUtilities_SQLServer : StructureUtilities
    {
        public StructureUtilities_SQLServer(GenConnection dbCon) : base(dbCon)
        {
        }

        public override SchemaStructure StructureFromDatabase(GenConnection dbCon, String name)
        {            
            SchemaStructure schema = new SchemaStructure(name);

            return schema;
        }
    }
}
