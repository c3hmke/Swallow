/**====================================================================================================================================**\
  ⠈⠙⠻⢿⣷⣶⣦⣤⣄⣀
       ⠉⠻⢿⣿⣿⣿⣿⣷⣦⣄          ▄█████ ▄▄   ▄▄  ▄▄▄  ▄▄    ▄▄     ▄▄▄  ▄▄   ▄▄   ██▄  ▄██ ▄▄  ▄▄▄▄ ▄▄▄▄   ▄▄▄ ▄▄▄▄▄▄ ▄▄  ▄▄▄  ▄▄  ▄▄  ▄▄▄▄
           ⠹⣿⣿⣿⣿⣿⣿⣷⡄       ▀▀▀▄▄▄ ██ ▄ ██ ██▀██ ██    ██    ██▀██ ██ ▄ ██   ██ ▀▀ ██ ██ ██ ▄▄ ██▄█▄ ██▀██  ██   ██ ██▀██ ███▄██ ███▄▄
             ⠸⣿⣿⣿⣿⣿⣿⣷⣶⣶⣶⣄ █████▀  ▀█▀█▀  ██▀██ ██▄▄▄ ██▄▄▄ ▀███▀  ▀█▀█▀    ██    ██ ██ ▀███▀ ██ ██ ██▀██  ██   ██ ▀███▀ ██ ▀██ ▄▄██▀
            ⢀⣼⣿⣿⣿⣿⣿⣿⣿⣿⣿⡿⠋                      ⢀⡀ ⡀⣀ ⠄ ⣀⡀   ⠄ ⣰⡀   ⣇⡀ ⡀⢀   ⣰⡀ ⣇⡀ ⢀⡀   ⣇⡀ ⡀⢀ ⢀⣀ ⡇⡠
        ⣀⣠⣶⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⡀                       ⣑⡺ ⠏  ⠇ ⡧⠜   ⠇ ⠘⠤   ⠧⠜ ⣑⡺   ⠘⠤ ⠇⠸ ⠣⠭   ⠇⠸ ⠣⠼ ⠭⠕ ⠏⠢
⣀⣀⣤⣤⣶⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿
   ⠉⢻⣿⣿⣿⣿⠿⠛⠋⠉⠉⠉⠉⠻⣿⣿⣿⣿⣿
   ⣠⡾⠟⠋  ⢀⣤⣶⣶⣤⡀  ⠸⣿⣿⣿⣿
⠔⠊⠁    ⣾⣿     ⣿⣷  ⢿⣿⣿⡟
       ⠻⣿⣦⣤⣤⣴⣿⠟  ⢸⣿⣿⠁
         ⠈⠛⠛⠋    ⢸⣿⠃
                 ⢸⠃
\**====================================================================================================================================**/

using System.Reflection;
using Npgsql;

namespace Swallow;

internal static class Program
{
    /// <summary>
    /// Parse the command line arguments and act accordingly.
    /// </summary>
    private static int Main(string[] args)
    {
        // if no command is provided, print the help statement and exit.
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }
        
        // otherwise get the command and the arguments which follow
        string command = args[0].Trim().ToLower();

        switch (command)
        {
            case "migrate":
            {
                (string connectionString, string migrationsPath) = ParseArgs(args.Skip(1).ToArray());
                RunMigrations(connectionString, migrationsPath);
                return 0;
            }

            case "-h": case "--help":
            {
                PrintHelp();
                return 0;
            }
            
            case "-v": case "--version":
            {
                // Get the version number from the assembly
                var version = Assembly
                              .GetExecutingAssembly()
                              .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                              .InformationalVersion
                              ?? "unknown";
                
                // Only print the version portion, not the commit hash
                Console.WriteLine(version.Split('+')[0]);
                return 0;
            }

            default:
            {
                Console.Error.WriteLine($"Unknown command: {command}");
                PrintHelp();
                return 1;
            }
        }
    }
    
    /// <summary>
    /// Print the help statement to the console.
    /// </summary>
    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Swallow Migrations ོ - grip it by the husk

            Usage:
              swallow migrate [options]       | Run the migrations
              swallow --help / -h             | Display the help message
              swallow --version / -v          | Display the tool version

            Options (for 'migrate'):
              -c, --conn <connectionString>   (or env: SWALLOW_CONNECTION_STRING)
              -p, --path <migrationsPath>     (or env: SWALLOW_MIGRATIONS_PATH)
              
              * options provided via command line will override env vars *

            Examples:
              swallow migrate --conn "<CONNECTION_STRING>" --path "./migrations"
              SWALLOW_CONNECTION_STRING="<CONNECTION_STRING>" SWALLOW_MIGRATIONS_PATH="./migrations" swallow migrate
            """);
    }

    /// <summary>
    /// Actually runs the migrations.
    /// This so that the main program body can focus on the interpretation of commands.
    /// </summary>
    private static void RunMigrations(string connectionString, string migrationsPath)
    {
        // Open an SQL connection
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        
        // Go through each file in the provided directory alphabetically.
        var last = GetLastMigration(conn);
        foreach (var file in Directory.EnumerateFiles(migrationsPath, "*.sql").OrderBy(f => f))
        {
            // Skip any files we have already migrated.
            if (last != null && StringComparer.Ordinal.Compare(Path.GetFileName(file), last) <= 0)
                continue;
            
            // Read the file and get the statements within
            var sql = File.ReadAllText(file);
            var statements = SplitStatements(sql);
            
            // Begin a transaction to execute the statements
            using var transaction = conn.BeginTransaction();
            try
            {
                using var batch = new NpgsqlBatch(conn);
                foreach (var statement in statements)
                {
                    batch.BatchCommands.Add(new NpgsqlBatchCommand(statement));
                }
            
                batch.ExecuteNonQuery();
                transaction.Commit();
        
                RecordMigration(conn, Path.GetFileName(file), sql);
                Console.WriteLine($"✔ {Path.GetFileName(file)} completed");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"✘ {Path.GetFileName(file)} failed: {e.Message}");
                transaction.Rollback();
                break;
            }
        }
    }

    /// <summary>
    /// Parses both the ENV and any command line arguments to get the necessary variables to run the migrations.
    /// This can use a mix of both, but command line args will override ENV variables, if a complete set can't
    /// be parsed, this will cause the program to exit with status code 2.
    /// </summary>
    private static (string connectionString, string migrationsPath) ParseArgs(string[] args)
    {
        // Pull necessary arguments from the Environment variables
        string? connectionString  = Environment.GetEnvironmentVariable("SWALLOW_CONNECTION_STRING");
        string? migrationsPath    = Environment.GetEnvironmentVariable("SWALLOW_MIGRATIONS_PATH");
        
        // Parse CLI args and override the environment if any are present.
        for (int i = 0; i < args.Length; i++)
        {
            // This switch will check for any of the argument flags. When one is found at args[i], the value
            // for that flag will be at args[i+1], so it grabs that value then increments `i` accordingly.
            // It will simply ignore any argument not listed below.
            switch (args[i])
            {
                case "-c": case "--conn":
                {
                    if (i + 1 < args.Length)
                    {
                        connectionString = args[++i];
                    }
                    else { Console.Error.WriteLine("Missing value for --conn"); Environment.Exit(2); }
                    break;
                }
                
                case "-p": case "--path":
                {
                    if (i + 1 < args.Length)
                    {
                        migrationsPath = args[++i];
                    }
                    else { Console.Error.WriteLine("Missing value for --path"); Environment.Exit(2); }
                    break;
                }
            }
        }
        
        // Neither the ENV nor args contained the required info, fail and exit
        if (connectionString is null || migrationsPath is null)
        {
            if (connectionString is null) Console.Error.WriteLine("missing: SWALLOW_CONNECTION_STRING");
            if (migrationsPath is null)   Console.Error.WriteLine("missing: SWALLOW_MIGRATIONS_PATH");
            
            Environment.Exit(2); // missing required parameter or env var
        }
        
        return (connectionString, migrationsPath);
    }
    
    /// <summary>
    /// Splits a SQL query string into separate statements if present.
    /// 
    /// This is done so that they can be executed as a batch rather than
    /// as a single statement which generally will trip up.
    /// </summary>
    private static IEnumerable<string> SplitStatements(string sql)
    {
        foreach (var s in sql.Split(';'))
        {
            var trimmed = s.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    /// <summary>
    /// Record a migration that has been run to the database in the __migrations table.
    /// </summary>
    private static void RecordMigration(NpgsqlConnection conn, string filename, string sql)
    {
        // Generate a hash of the SQL that was executed for this migration.
        byte[] hash  = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql));
        
        // Create the command that will be executed, then add variables.
        using var cmd = new NpgsqlCommand(
            "INSERT INTO __migrations (filename, date_executed, checksum)" +
            "VALUES (@filename, NOW(), @checksum)"
            , conn);
        
        cmd.Parameters.AddWithValue("filename", filename);
        cmd.Parameters.AddWithValue("checksum", Convert.ToHexString(hash));
        
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Returns the filename for the last run migration, or null if none have been run yet.
    /// </summary>
    private static string? GetLastMigration(NpgsqlConnection conn)
    {
        using var cmd = new NpgsqlCommand("SELECT filename FROM __migrations ORDER BY filename DESC LIMIT 1", conn);
        
        return cmd.ExecuteScalar() as string;
    }
}