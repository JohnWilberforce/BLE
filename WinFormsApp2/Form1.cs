using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace WinFormsApp2
{
    public partial class Form1 : Form
    {
        GattServiceProvider serviceProvider;
        private GattLocalCharacteristic ResultCharacteristic;

        public static readonly Guid uuid = Guid.Parse("aaaaaaaa-e1d9-11e6-bf01-fe55135034f0");

        public static readonly Guid uuid1 = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f1");
        public static readonly Guid uuid2 = Guid.Parse("caec2ebc-e1d9-11e6-bf01-fe55135034f2");

        public static readonly GattLocalCharacteristicParameters gattResultParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read |
                                          GattCharacteristicProperties.Write |
                                        GattCharacteristicProperties.Notify,
            WriteProtectionLevel = GattProtectionLevel.Plain,
            ReadProtectionLevel = GattProtectionLevel.Plain,
            UserDescription = "Result Characteristic"
        };

        public Form1()
        {
            InitializeComponent();
            textBox1.KeyDown += textBox1_KeyDown;
            //ServiceProviderInitAsync();
        }
        private async void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (serviceProvider == null)
                {
                    var serviceStarted = await ServiceProviderInitAsync();
                    if (serviceStarted)
                    {
                        Debug.WriteLine("Service started");
                    }
                }
            }
        }
        private async Task<bool> ServiceProviderInitAsync()
        {
            // BT_Code: Initialize and starting a custom GATT Service using GattServiceProvider.
            GattServiceProviderResult serviceResult = await GattServiceProvider.CreateAsync(uuid);
            if (serviceResult.Error == BluetoothError.Success)
            {
                serviceProvider = serviceResult.ServiceProvider;
            }
            GattLocalCharacteristicResult result = await serviceProvider.Service.CreateCharacteristicAsync(uuid1, gattResultParameters);
            if (result.Error == BluetoothError.Success)
            {
                ResultCharacteristic = result.Characteristic;
            }



            ResultCharacteristic.ReadRequested += ResultCharacteristic_ReadRequestedAsync;
            ResultCharacteristic.SubscribedClientsChanged += ResultCharacteristic_SubscribedClientsChanged;
            ResultCharacteristic.WriteRequested += ResultCharacteristic_WriteRequestedAsync;





            // BT_Code: Indicate if your sever advertises as connectable and discoverable.
            GattServiceProviderAdvertisingParameters advParameters = new GattServiceProviderAdvertisingParameters
            {

                IsConnectable = true,

                IsDiscoverable = true
            };
            serviceProvider.AdvertisementStatusChanged += ServiceProvider_AdvertisementStatusChanged;

            serviceProvider.StartAdvertising(advParameters);
            Debug.WriteLine("Service started");
            return true;
        }
        Dictionary<string, string> clientMessageMap = new Dictionary<string, string>();
        private async void ResultCharacteristic_SubscribedClientsChanged(GattLocalCharacteristic sender, object args)
        {
            if (ResultCharacteristic == null || ResultCharacteristic.SubscribedClients.Count == 0)
            {
                return;
            }
            var writer = new DataWriter();
            writer.ByteOrder = ByteOrder.LittleEndian;
            writer.WriteString(textBox1.Text);
            foreach (var client in ResultCharacteristic.SubscribedClients)
            {
                var clientId = client.Session.DeviceId.ToString();
                clientMessageMap.TryGetValue(clientId, out var lastmessage);
                if (!string.IsNullOrEmpty(lastmessage))
                {
                    if (lastmessage.ToLower() == textBox1.Text)
                    {
                        continue;
                    }
                    clientMessageMap[clientId] = textBox1.Text;
                }

                clientMessageMap[clientId] = textBox1.Text;
            }
            Debug.WriteLine($" subscribed clients {sender.SubscribedClients.Count}");
            var nResult = await ResultCharacteristic.NotifyValueAsync(writer.DetachBuffer());
        }
        private void ServiceProvider_AdvertisementStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
        {
            // Created - The default state of the advertisement, before the service is published for the first time.
            // Stopped - Indicates that the application has canceled the service publication and its advertisement.
            // Started - Indicates that the system was successfully able to issue the advertisement request.
            // Aborted - Indicates that the system was unable to submit the advertisement request, or it was canceled due to resource contention.
            Debug.WriteLine("Advertisement Status: " + sender.AdvertisementStatus);
        }
        private async void ResultCharacteristic_ReadRequestedAsync(GattLocalCharacteristic sender, GattReadRequestedEventArgs args)
        {
            // BT_Code: Process a read request. 
            using (args.GetDeferral())
            {
                // Get the request information.  This requires device access before an app can access the device's request. 
                GattReadRequest request = await args.GetRequestAsync();

                var writer = new DataWriter();
                writer.ByteOrder = ByteOrder.LittleEndian;
                writer.WriteString(textBox1.Text);
                Console.WriteLine(textBox1.Text);
                // Can get details about the request such as the size and offset, as well as monitor the state to see if it has been completed/cancelled externally.
                // request.Offset
                // request.Length
                // request.State
                // request.StateChanged += <Handler>

                // Gatt code to handle the response
                request.RespondWithValue(writer.DetachBuffer());
            }
        }
        private async Task SendNotification(string message)
        {
            if (ResultCharacteristic != null)
            {
                var writer = new DataWriter();
                writer.ByteOrder = ByteOrder.LittleEndian;
                writer.WriteString(message);
                await ResultCharacteristic.NotifyValueAsync(writer.DetachBuffer());
                Console.WriteLine($"sent back to central: {message}");
                Debug.WriteLine($"sent back to central: {message}");
            }
        }
        private async void ResultCharacteristic_WriteRequestedAsync(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
        {
            // BT_Code: Process a read request. 
            using (args.GetDeferral())
            {
                //var deviceIdArray = (args.Session.DeviceId.Id).Split('#');
                //var deviceId = deviceIdArray[deviceIdArray.Length - 1];

                var deviceId = (args.Session.DeviceId.Id);
                var blAddr = "";
                if (deviceId.Contains('-'))
                {
                    blAddr = deviceId.Split('-').Last();
                }
                    DeviceInformation deviceInfo = await DeviceInformation.CreateFromIdAsync(deviceId);
                    var devName = deviceInfo.Name ?? "Unknown Device";

                    // Get the request information.  This requires device access before an app can access the device's request.
                    GattWriteRequest request = await args.GetRequestAsync();
                    if (request == null)
                    {
                        // No access allowed to the device.  Application should indicate this to the user.
                        return;
                    }
                    if (request.State == GattRequestState.Pending)
                    {
                        var reader = DataReader.FromBuffer(request.Value);
                        var receivedData = reader.ReadString(reader.UnconsumedBufferLength);
                        textBox2.BeginInvoke((MethodInvoker)(() =>
                        {
                            //textBox2.Text = $"{receivedData}{Environment.NewLine}From: {deviceId}";
                            textBox2.Text = $"Read Quote: {receivedData}{Environment.NewLine}From:Client Address: {blAddr} | Device Name: {devName}";
                        }));

                        textBox3.BeginInvoke((MethodInvoker)(() =>
                        {
                            textBox3.Text = $"Read Quote: {receivedData}";
                        }));

                        await SendNotification($"Response: {receivedData}");

                    }
                    else
                    {
                        return;
                    }

                }
            }
        }
    }


