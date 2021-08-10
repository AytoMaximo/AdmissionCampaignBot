using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdmissionCampaign
{
    public class AsciiTableGenerator
    {
        public static StringBuilder CreateAsciiTableFromDataTable(List<Task<DataHandler.ParsedData>> data, DataHandler.ParsedDataList lastReport)
        {
            var table = CreateDataTable(data, lastReport);

            var lenghtByColumnDictionary = GetTotalSpaceForEachColumn(table);

            var tableBuilder = new StringBuilder();
            AppendColumns(table, tableBuilder, lenghtByColumnDictionary);
            AppendRows(table, lenghtByColumnDictionary, tableBuilder);
            return tableBuilder;
        }

        private static DataTable CreateDataTable(List<Task<DataHandler.ParsedData>> data, DataHandler.ParsedDataList lastReport)
         {
             var table = new DataTable("Статистика приемной кампании");
             DataColumn dtColumn;  
             DataRow myDataRow;
             
             //Code
             dtColumn = new DataColumn();
             dtColumn.DataType = typeof(string);
             dtColumn.ColumnName = "Код";
             table.Columns.Add(dtColumn);

             //Бюджет
             dtColumn = new DataColumn();
             dtColumn.DataType = typeof(string);
             dtColumn.ColumnName = "Мест";
             table.Columns.Add(dtColumn);
             
             //Согласий
             dtColumn = new DataColumn();
             dtColumn.DataType = typeof(string);
             dtColumn.ColumnName = "Согласий";
             table.Columns.Add(dtColumn);
             
             //Динамика
             dtColumn = new DataColumn();
             dtColumn.DataType = typeof(string);
             dtColumn.ColumnName = "Динамика";
             table.Columns.Add(dtColumn);
             
             //Балл
             dtColumn = new DataColumn();
             dtColumn.DataType = typeof(string);
             dtColumn.ColumnName = "П/Балл";
             table.Columns.Add(dtColumn);

             foreach (var record in data.Select(task => task.Result))
             {
                 myDataRow = table.NewRow();
                 myDataRow["Код"] = BaseData.SpecialityTitleShort[record.Specialty];
                 myDataRow["Мест"] = record.MaxCount;
                 myDataRow["Согласий"] = record.CurrentCount;
                 
                 var lastSpecialityMetrics =
                     lastReport?.Data.SingleOrDefault(d => d.Specialty == record.Specialty);

                 myDataRow["Динамика"] = 0;
                 if (lastSpecialityMetrics != null)
                 {
                     if (lastSpecialityMetrics.CurrentCount < record.CurrentCount)
                     {
                         myDataRow["Динамика"]= $" (+{record.CurrentCount - lastSpecialityMetrics.CurrentCount})";
                     }
                     else if (lastSpecialityMetrics.CurrentCount > record.CurrentCount)
                     {
                         myDataRow["Динамика"]= $" (-{lastSpecialityMetrics.CurrentCount - record.CurrentCount})";
                     }
                 }

                 myDataRow["П/Балл"] = record.AcceptedScore;
                 
                 table.Rows.Add(myDataRow);
             }
             
             return table;
         }

        private static void AppendRows(DataTable table, IReadOnlyDictionary<int, int> lenghtByColumnDictionary,
            StringBuilder tableBuilder)
        {
            for (var i = 0; i < table.Rows.Count; i++)
            {
                var rowBuilder = new StringBuilder();
                for (var j = 0; j < table.Columns.Count; j++)
                {
                    rowBuilder.Append(PadWithSpaceAndSeperator(table.Rows[i][j].ToString()?.Trim(),
                        lenghtByColumnDictionary[j]));
                }
                tableBuilder.AppendLine(rowBuilder.ToString());
            }
        }

        private static void AppendColumns(DataTable table, StringBuilder builder,
            IReadOnlyDictionary<int, int> lenghtByColumnDictionary)
        {
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var columName = table.Columns[i].ColumnName.Trim();
                var paddedColumNames = PadWithSpaceAndSeperator(ToTitleCase(columName), lenghtByColumnDictionary[i]);
                builder.Append(paddedColumNames);
            }
            builder.AppendLine();
            builder.AppendLine(string.Join("", Enumerable.Repeat("-", builder.ToString().Length - 3).ToArray()));
        }

        private static string ToTitleCase(string columnName)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(columnName.Replace("_", " "));
        }

        private static Dictionary<int, int> GetTotalSpaceForEachColumn(DataTable table)
        {
            var lengthByColumn = new Dictionary<int, int>();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var length = new int[table.Rows.Count];
                for (var j = 0; j < table.Rows.Count; j++)
                {
                    length[j] = table.Rows[j][i].ToString().Trim().Length;
                }
                lengthByColumn[i] = length.Max();
            }
            return CompareToColumnNameLengthAndUpdate(table, lengthByColumn);
        }

        private static Dictionary<int, int> CompareToColumnNameLengthAndUpdate(DataTable table,
            IReadOnlyDictionary<int, int> lenghtByColumnDictionary)
        {
            var dictionary = new Dictionary<int, int>();
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var columnNameLength = table.Columns[i].ColumnName.Trim().Length;
                dictionary[i] = columnNameLength > lenghtByColumnDictionary[i]
                    ? columnNameLength
                    : lenghtByColumnDictionary[i];
            }
            return dictionary;
        }

        private static string PadWithSpaceAndSeperator(string value, int totalColumnLength)
        {
            var remaningSpace = value.Length < totalColumnLength
                ? totalColumnLength - value.Length
                : value.Length - totalColumnLength;
            var spaces = string.Join("", Enumerable.Repeat(" ", remaningSpace).ToArray());
            return value + spaces + " | ";
        }
    }
}