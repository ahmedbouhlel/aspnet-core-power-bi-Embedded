using PowerBIEmbedded_AppOwnsData.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace PowerBIEmbedded_AppOwnsData.Context
{
    public class DBConnector
    {

        private string connectionString = string.Empty;


        public DBConnector()
        {
            connectionString = ConfigurationManager.ConnectionStrings["DbConnectionString"].ConnectionString;
        }

        public ReportInfoModel GetReportInfo(int CampaignId , string LanguageId)
        {
            ReportInfoModel model = new ReportInfoModel();
            SqlDataReader reader;
            string result = string.Empty;

            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    using (SqlCommand comm = new SqlCommand("[digitalroom].[PS_GetCampaignReport]"))
                    {
                        comm.Connection = connection;
                        comm.CommandType = CommandType.StoredProcedure;

                        comm.Parameters.AddWithValue("CampaignID", CampaignId);
                        comm.Parameters.AddWithValue("@languageID",LanguageId);
                        comm.Parameters.AddWithValue("@ERROR_CODE", 0);

                        connection.Open();
                        reader = comm.ExecuteReader();
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal("ReportToken")))
                                    model.ReportId = reader["ReportToken"].ToString();
                            }

                            return model;
                        }
                        connection.Close();
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"SQL Error: {ex.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error:{e.Message}");
                }
            }
            return null;
        }

    }
}