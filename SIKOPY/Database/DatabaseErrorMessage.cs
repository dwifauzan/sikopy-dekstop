using System;
using System.Net.Sockets;
using Npgsql;

namespace SIKOPY.Database;

/// <summary>Turns database exceptions into short Indonesian messages for the UI.</summary>
public static class DatabaseErrorMessage
{
    public static string Describe(Exception exception) => exception switch
    {
        // Thrown by PostgresConnectionFactory when no connection string is configured.
        InvalidOperationException { Message: var message } when message.StartsWith("No database connection is configured", StringComparison.Ordinal) =>
            $"Koneksi database belum diatur. Buat file {PostgresConnectionFactory.LocalSettingsFileName} " +
            $"(lihat database/README.md) atau isi variabel {PostgresConnectionFactory.ConnectionStringEnvironmentVariable}, lalu jalankan ulang aplikasi.",
        InvalidOperationException { Message: var message } when message.Contains(PostgresConnectionFactory.LocalSettingsFileName, StringComparison.Ordinal) =>
            $"File {PostgresConnectionFactory.LocalSettingsFileName} tidak valid. Periksa penulisan JSON-nya.",

        PostgresException { SqlState: "3D000" } =>
            "Database tidak ditemukan. Periksa nama database pada koneksi.",
        PostgresException { SqlState: "28P01" or "28000" } =>
            "Login ke database gagal. Periksa username dan password pada koneksi.",
        PostgresException { SqlState: "42P01" or "42703" } =>
            "Struktur database belum sesuai. Jalankan database/schema.sql atau migrasi terbaru.",
        PostgresException { SqlState: var state, MessageText: var text } when state.StartsWith("23", StringComparison.Ordinal) =>
            $"Data ditolak oleh database: {text}",
        PostgresException { MessageText: var text } =>
            $"Terjadi kesalahan database: {text}",

        NpgsqlException { InnerException: SocketException or TimeoutException } or NpgsqlException { IsTransient: true } =>
            "Tidak dapat terhubung ke database. Pastikan PostgreSQL berjalan, lalu coba lagi.",

        _ => exception.Message
    };
}
