﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.Azure.Devices.Client;
using System.Threading;
using Newtonsoft.Json;
using System.Configuration;

namespace simulator
{
    class Program
    {
        static RegistryManager registryManager;
        static string connectionString = "";
        static string iotHubUri = "";
        static string deviceId = null;
        static string deviceKey = null;
        static DeviceClient deviceClient = null;
        static Random random = new Random();
        static int max = 100;
        static int min = 0;
        /// <summary>
        /// Simulator a device
        /// </summary>
        /// <param name="args">Usage: sumulator {deviceid} {min} {max}</param>
        static void Main(string[] args)
        {
            #region added
            if (args.Length < 3)
            {
                Error("Usage: sumulator {deviceid} {max} {min}");
                Wait();
                return;
            }
            deviceId = args[0];
            max = int.Parse(args[2]);
            min = int.Parse(args[1]);

            connectionString = ConfigurationManager.AppSettings["iotHubConnectionString"];
            iotHubUri = ConfigurationManager.AppSettings["iotHubUri"];

            registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            #endregion
            AddDeviceAsync().Wait();

#if true
            //AMQP (default)
            deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, deviceKey));
#else
            //HTTPS
            deviceClient = DeviceClient.Create(iotHubUri, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, deviceKey),
                                                Microsoft.Azure.Devices.Client.TransportType.Http1);
#endif
            SendDeviceToCloudMessagesAsync();
            ReceiveCommandAsync();
            Wait("Press [ENTER] to exit...");

            RemoveDeviceAsync().Wait();
        }
        static string GenerateMessage(int seq, string message)
        {
            var msg = TelemetryData.Random(deviceId, string.Format("{0}{1}", DateTime.UtcNow.ToString("yyyymmdd"), seq.ToString("0000000")), message, min, max);
            return JsonConvert.SerializeObject(msg);
        }
        static void Wait(string msg = null)
        {
            if (string.IsNullOrEmpty(msg))
            {
                Log("Press [ENTER] to continue...");
            }
            else
            {
                Log(msg);
            }
            Console.ReadLine();
        }
        
        static void Error(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
        static void Success(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
        static void Log(string msg)
        {
            Console.ResetColor();
            Console.WriteLine(msg);
        }
        private async static Task RemoveDeviceAsync()
        {
            var device = await registryManager.GetDeviceAsync(deviceId);
            await registryManager.RemoveDeviceAsync(device);
        }
        private async static Task AddDeviceAsync()
        {
            Device device;
            try
            {
                device = await registryManager.AddDeviceAsync(new Device(deviceId));
            }
            catch (DeviceAlreadyExistsException)
            {
                device = await registryManager.GetDeviceAsync(deviceId);
            }
            deviceKey = device.Authentication.SymmetricKey.PrimaryKey;
            Log($"device id {deviceId} : {deviceKey}");
        }

        private static async void SendDeviceToCloudMessagesAsync()
        {
            int i = 0;
            while (true)
            {
                i++;
                string telemetry = GenerateMessage(i, $"message:{i}");
                await deviceClient.SendEventAsync(new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(telemetry)));
                Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, telemetry);

                Thread.Sleep(3000);
            }
        }

        private async static void ReceiveCommandAsync()
        {
            while (true)
            {
                var cmd = await deviceClient.ReceiveAsync();
                if (cmd != null)
                {
                    Success(Encoding.UTF8.GetString(cmd.GetBytes()));
                    await deviceClient.CompleteAsync(cmd);
                }

                Thread.Sleep(1000);
            }
        }
    }
}
