using System;
using Npgsql;

var connString = "Host=localhost;Port=59317;Database=adms;Username=postgres;Password=postgres";

try
{
    using var conn = new NpgsqlConnection(connString);
    conn.Open();

    using var cmd = new NpgsqlCommand("SELECT \"Id\", \"Status\", \"LabelId\", \"ClassificationConfidence\", \"ExtractedMetadata\", \"ProcessingMessage\" FROM \"Documents\" ORDER BY \"CreatedAt\" DESC LIMIT 1;", conn);
    using var reader = cmd.ExecuteReader();
    if (reader.Read())
    {
        Console.WriteLine($"Id: {reader["Id"]}");
        Console.WriteLine($"Status: {reader["Status"]}");
        Console.WriteLine($"LabelId: {reader["LabelId"]}");
        Console.WriteLine($"ClassificationConfidence: {reader["ClassificationConfidence"]}");
        Console.WriteLine($"ProcessingMessage: {reader["ProcessingMessage"]}");
        Console.WriteLine($"ExtractedMetadata: {reader["ExtractedMetadata"]}");
    }
    else
    {
        Console.WriteLine("No documents found.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
