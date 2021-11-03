using ApiClient;
using ApiClient.Model;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DreamhostDDNSClient
{
    internal class Program
    {
        private static DnsApiProcessor _client;

        private static bool _addressesMatch;

        private static readonly ListResponseModel _editableEntries = new();

        private static readonly string _domain = "example.com";
        private static readonly string _recordType = "A";

        private static readonly Uri _ipService = new("http://ipinfo.io/ip");

        private static void Main()
            => Entry().GetAwaiter().GetResult();

        private static async Task Entry()
        {
            _client = new("6SHU5P2HLDAYECUM");

            try
            {
                while (true)
                {
                    #region Get IP addresses
                    IPAddress clientIp = await GetClientIpAsync();
                    IPAddress dnsIp = null;
                    try
                    {
                        dnsIp = await GetDnsIpAsync();
                    }
                    catch (Exception ex) when (ex is not NullReferenceException)
                    {
                        Console.Error.WriteLine(ex.Message);
                    }
                    #endregion

                    #region Match IP addressess
                    _addressesMatch = clientIp.Equals(dnsIp);

                    if (!_addressesMatch)
                    {
                        await _client.RemoveRecord(_domain, Enum.Parse<RecordType>(_recordType), dnsIp.ToString());
                        await _client.AddRecord(_domain, Enum.Parse<RecordType>(_recordType), clientIp.ToString());
                    }
                    #endregion

                    Thread.Sleep(TimeSpan.FromMinutes(5));
                }
            }
            catch (NullReferenceException)
            {
                #region Restart process
                Process process = Process.GetCurrentProcess();
                Process.Start(process.MainModule.FileName);

                Environment.Exit(-1);
                #endregion
            }
        }

        private static async Task<IPAddress> GetClientIpAsync()
        {
            using WebClient client = new();
            Stream stream = await client.OpenReadTaskAsync(_ipService);
            using StreamReader ipStream = new(stream);
            return IPAddress.Parse(ipStream.ReadToEnd().Trim());
        }

        private static async Task<IPAddress> GetDnsIpAsync()
        {
            ListResponseModel responseModel = await _client.ListRecords();

            if (responseModel.Reason != null)
                throw new Exception($"Couldn't list records: {responseModel.Reason}");

            _editableEntries.Data = responseModel.Data.Where(entry => entry.Editable == 1 && entry.Record == _domain && entry.Type == _recordType).ToList();

            return _editableEntries.Data.Count == 1
                ? IPAddress.Parse(_editableEntries.Data[0].Value)
                : throw new Exception("Couldn't find an applicable DNS entry");
        }
    }
}