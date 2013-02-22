/*
* Copyright (c) 2011 Dennis Mackay-Fisher
*
* This file is part of PV Scheduler
* 
* PV Scheduler is free software: you can redistribute it and/or 
* modify it under the terms of the GNU General Public License version 3 or later 
* as published by the Free Software Foundation.
* 
* PV Scheduler is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with PV Scheduler.
* If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace DatabaseUtilities
{
    public enum IndexType
    {
        PrimaryKey,
        UniqueKey,
        Index
    }

    public struct OrderItem
    {
        public ColumnStructure Column;
        public bool IsAscending;
    }

    public class IndexStructure
    {
        public SchemaStructure Schema { get; private set; }
        public IndexType IndexType { get; private set; }

        public TableStructure Table { get; private set; }

        public String Name { get; private set; }
        public List<OrderItem> Columns;

        public IndexStructure(SchemaStructure schema, IndexType indexType)
        {
            Schema = schema;
            Initialise(indexType);
        }

        private void Initialise(IndexType indexType)
        {
            IndexType = indexType;
            Table = null;
            Name = null;
            Columns = new List<OrderItem>();
        }

        public IndexStructure(SchemaStructure schema, IndexType indexType, XmlNode indexNode)
        {
            Schema = schema;
            Initialise(indexType);
            ParseIndex(indexNode);
        }

        public void ParseIndex(XmlNode indexNode)
        {
            Name = indexNode.Attributes["Name"].Value;

            XmlNode tableNode = indexNode.SelectSingleNode("Relationship[@Name = \"DefiningTable\"]/Entry/References");
            String tableName = tableNode.Attributes["Name"].Value;
            Table = Schema.FindTable(tableName);

            XmlNodeList nodes = indexNode.SelectNodes("Relationship[@Name = \"ColumnSpecifications\"]/Entry/Element[@Type = \"ISqlIndexedColumnSpecification\"]");
            foreach (XmlNode node in nodes)
            {
                XmlNode columnNode = node.SelectSingleNode("Relationship[@Name = \"Column\"]/Entry/References");
                String columnName = columnNode.Attributes["Name"].Value;
                ColumnStructure column = Table.FindColumn(columnName);

                OrderItem item;

                item.Column = column;
                item.IsAscending = true;

                Columns.Add(item);
            }
            Table.AddIndex(this);
        }
    }

    public class ForeignKeyStructure
    {
        public TableStructure Table { get; private set; }
        public List<ColumnStructure> Columns;

        public TableStructure ReferencedTable { get; private set; }
        public List<ColumnStructure> ReferencedColumns { get; private set; }

        public String Name { get; private set; }
        public bool CascadeDelete { get; private set; }

        public ForeignKeyStructure()
        {
            Initialise();
        }

        private void Initialise()
        {
            Table = null;
            ReferencedTable = null;
            Name = null;
            Columns = new List<ColumnStructure>();
            ReferencedColumns = new List<ColumnStructure>();
            CascadeDelete = false;
        }

        public ForeignKeyStructure(XmlNode foreignKeyNode, SchemaStructure schema)
        {
            Initialise();
            ParseForeignKey(foreignKeyNode, schema);
        }

        private void ParseForeignKey(XmlNode foreignKeyNode, SchemaStructure schema)
        {
            Name = foreignKeyNode.Attributes["Name"].Value;

            XmlNode tableNode = foreignKeyNode.SelectSingleNode("Relationship[@Name = \"DefiningTable\"]/Entry/References");
            String tableName = tableNode.Attributes["Name"].Value;
            Table = schema.FindTable(tableName);

            XmlNodeList nodes = foreignKeyNode.SelectNodes("Relationship[@Name = \"Columns\"]/Entry/References");
            foreach (XmlNode node in nodes)
            {
                String columnName = node.Attributes["Name"].Value;
                ColumnStructure column = Table.FindColumn(columnName);

                Columns.Add(column);
            }

            XmlNode foreignTableNode = foreignKeyNode.SelectSingleNode("Relationship[@Name = \"ForeignTable\"]/Entry/References");
            String foreignTableName = foreignTableNode.Attributes["Name"].Value;
            ReferencedTable = schema.FindTable(foreignTableName);

            nodes = foreignKeyNode.SelectNodes("Relationship[@Name = \"ForeignColumns\"]/Entry/References");
            foreach (XmlNode node in nodes)
            {
                String columnName = node.Attributes["Name"].Value;
                ColumnStructure column = ReferencedTable.FindColumn(columnName);

                Columns.Add(column);
            }
        }

    }

    public class ColumnStructure
    {
        public TableStructure Table { get; private set; }
        public String Name { get; private set; }
        public bool IsNullable { get; private set; }
        public bool IsIdentity { get; private set; }
        public String BaseType { get; private set; }
        public String Length { get; private set; }

        public ColumnStructure(TableStructure table)
        {
            Table = table;
            Initialise();
        }

        private void Initialise()
        {
            Name = "";
            IsNullable = true;
            IsIdentity = false;
            BaseType = "";
            Length = "";
        }

        public ColumnStructure(TableStructure table, XmlNode columnNode)
        {
            Table = table;
            Initialise();
            ParseColumn(columnNode);
        }

        private void ParseColumn(XmlNode columnNode)
        {
            Name = columnNode.Attributes["Name"].Value;

            {
                XmlNode nullable = columnNode.SelectSingleNode("Property[@Name = \"IsNullable\"]");
                if (nullable != null)
                    IsNullable = (nullable.Attributes["Value"].Value != "False");
            }
            {
                XmlNode identity = columnNode.SelectSingleNode("Property[@Name = \"IsIdentity\"]");
                if (identity != null)
                    IsIdentity = (identity.Attributes["Value"].Value == "True");
            }
            {
                XmlNode type = columnNode.SelectSingleNode("Relationship[@Name = \"TypeSpecifier\"]/Entry/Element[@Type = \"ISql90TypeSpecifier\"]");

                if (type != null)
                {
                    XmlNode typeDetail = type.SelectSingleNode("Relationship[@Name = \"Type\"]/Entry/References[@ExternalSource = \"BuiltIns\"]");
                    if (typeDetail != null)
                        BaseType = typeDetail.Attributes["Name"].Value;

                    XmlNode lengthNode = type.SelectSingleNode("Property[@Name = \"Length\"]");
                    if (lengthNode != null)
                        Length = lengthNode.Attributes["Value"].Value;

                }
            }
        }
    }

    public class TableStructure
    {
        public SchemaStructure Schema { get; private set; }
        public String Name { get; private set; }
        public List<ColumnStructure> Columns { get; private set; }
        public IndexStructure PrimaryKey;
        public List<IndexStructure> Indexes;

        public TableStructure(SchemaStructure schema)
        {
            Schema = schema;
            Initialise();
        }

        private void Initialise()
        {
            Columns = new List<ColumnStructure>();
            Indexes = new List<IndexStructure>();
            PrimaryKey = null;
        }

        public TableStructure(SchemaStructure schema, XmlNode tableNode)
        {
            Schema = schema;
            Initialise();
            ParseTable(tableNode);
        }

        private void ParseTable(XmlNode tableNode)
        {
            Name = tableNode.Attributes["Name"].Value;
            XmlNodeList nodes = tableNode.SelectNodes("Relationship[@Name = \"Columns\"]/Entry/Element[@Type = \"ISql100SimpleColumn\"]");
            foreach (XmlNode node in nodes)
            {
                ColumnStructure column = new ColumnStructure(this, node);
                Columns.Add(column);
            }
        }

        public void AddIndex(IndexStructure index)
        {
            if (index.IndexType == IndexType.PrimaryKey)
                PrimaryKey = index;
            Indexes.Add(index);
        }

        public ColumnStructure FindColumn(String columnName)
        {
            foreach (ColumnStructure column in Columns)
            {
                if (column.Name == columnName)
                    return column;
            }
            return null;
        }
    }

    public class SchemaStructure
    {
        public String Name { get; private set; }
        public List<TableStructure> Tables { get; private set; }
        public List<IndexStructure> Indexes { get; private set; }
        public List<ForeignKeyStructure> ForeignKeys { get; private set; }

        public SchemaStructure(String name)
        {
            Name = name;
            Initialise();
        }

        private void Initialise()
        {
            Tables = new List<TableStructure>();
            Indexes = new List<IndexStructure>();
            ForeignKeys = new List<ForeignKeyStructure>();
        }

        public SchemaStructure(String name, XmlDocument structureDoc)
        {
            Name = name;
            Initialise();

            ParseSchema(structureDoc);
        }

        private void ParseSchema(XmlDocument structureDoc )
        {
            XmlNodeList tableNodes = structureDoc.SelectNodes("//Element[@Type = \"ISql100Table\"]");

            foreach (XmlNode node in tableNodes)
            {
                TableStructure table = new TableStructure(this, node);
                Tables.Add(table);
            }

            XmlNodeList primaryKeyNodes = structureDoc.SelectNodes("//Element[@Type = \"ISql100PrimaryKeyConstraint\"]");

            foreach (XmlNode node in primaryKeyNodes)
            {
                IndexStructure primaryKey = new IndexStructure(this, IndexType.PrimaryKey, node);
                Indexes.Add(primaryKey);
            }

            XmlNodeList uniqueNodes = structureDoc.SelectNodes("//Element[@Type = \"ISql100UniqueConstraint\"]");

            foreach (XmlNode node in uniqueNodes)
            {
                IndexStructure uniqueConstraint = new IndexStructure(this, IndexType.UniqueKey, node);
                Indexes.Add(uniqueConstraint);
            }

            XmlNodeList foreignKeyNodes = structureDoc.SelectNodes("//Element[@Type = \"ISql90ForeignKeyConstraint\"]");

            foreach (XmlNode node in foreignKeyNodes)
            {
                ForeignKeyStructure foreignKey = new ForeignKeyStructure(node, this);
                ForeignKeys.Add(foreignKey);
            } 
        }

        public TableStructure FindTable(String tableName)
        {
            foreach (TableStructure table in Tables)
            {
                if (table.Name == tableName)
                    return table;
            }
            return null;
        }
    }

    public class DBStructure
    {
        private String StructureFileName;
        public XmlDocument StructureDoc { get; private set; }

        public DBStructure(String structureFileName)
        {
            StructureFileName = structureFileName;
            StructureDoc = LoadStructure(structureFileName);
        }

        private static XmlDocument LoadStructure(String structureFileName)
        {
            XmlReader reader;

            // Create the validating reader and specify DTD validation.
            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.DtdProcessing = DtdProcessing.Parse;
            readerSettings.ValidationType = ValidationType.None;

            reader = XmlReader.Create(structureFileName, readerSettings);

            XmlDocument doc = new XmlDocument();
            doc.Load(reader);
            reader.Close();

            return doc;
        }

    }
}
