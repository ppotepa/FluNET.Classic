using FluNET.Classic.Archive;
using FluNET.Classic.Cache;
using FluNET.Classic.Cache.Redis;
using FluNET.Classic.Cloud;
using FluNET.Classic.Core;
using FluNET.Classic.Crypto;
using FluNET.Classic.Csv;
using FluNET.Classic.Email;
using FluNET.Classic.Email.Graph;
using FluNET.Classic.Email.SendGrid;
using FluNET.Classic.Email.Smtp;
using FluNET.Classic.Identity;
using FluNET.Classic.Network;
using FluNET.Classic.Secrets;
using FluNET.Classic.Secrets.Aws;
using FluNET.Classic.Secrets.Azure;
using FluNET.Classic.Sql;
using FluNET.Classic.Sql.PostgreSql;
using FluNET.Classic.Sql.Sqlite;
using FluNET.Classic.Sql.SqlServer;
using FluNET.Classic.Standard;
using FluNET.Classic.Storage;
using FluNET.Classic.Storage.Azure;
using FluNET.Classic.Storage.FileSystem;
using FluNET.Classic.Storage.S3;
using FluNET.Classic.Web;
using FluNET.Classic.Xml;
using SystemModule = FluNET.Classic.System.SystemModule;

namespace FluNET.Classic.Ecosystem;

public static class EcosystemModules
{
    public static IReadOnlyList<ILanguageModule> Core() => StandardModules.Create();
    public static IReadOnlyList<ILanguageModule> Data() => Distinct(Core().Concat(new ILanguageModule[] { new CsvModule(), new XmlModule(), new SqlModule() }));
    public static IReadOnlyList<ILanguageModule> Infrastructure() => Distinct(Core().Concat(new ILanguageModule[] { new StorageModule(), new CacheModule(), new ArchiveModule(), new CryptoModule(), new EmailModule(), new NetworkModule(), new WebModule(), new SecretsModule(), new IdentityModule(), new FluNET.Classic.Random.RandomModule(), new SystemModule(), new CloudModule() }));
    public static IReadOnlyList<ILanguageModule> Providers() => Distinct(Infrastructure().Concat(Data()).Concat(new ILanguageModule[] { new FileSystemStorageModule(), new AzureBlobStorageModule(), new S3StorageModule(), new RedisCacheModule(), new SmtpEmailModule(), new SendGridEmailModule(), new GraphEmailModule(), new AzureSecretsModule(), new AwsSecretsModule(), new SqlServerModule(), new PostgreSqlModule(), new SqliteModule() }));
    public static IReadOnlyList<ILanguageModule> All() => Providers();
    private static IReadOnlyList<ILanguageModule> Distinct(IEnumerable<ILanguageModule> modules) => modules.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToArray();
}
