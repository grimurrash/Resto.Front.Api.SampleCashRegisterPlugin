using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Resto.Front.Api.Attributes.JetBrains;
using Resto.Front.Api.Data.Device.Settings;
using Resto.Front.Api.Devices;
using System.Windows.Forms;

namespace Resto.Front.Api.SampleCashRegisterPlugin
{
    internal sealed class SampleCashRegisterFactory : MarshalByRefObject, ICashRegisterFactory
    {
        // Note http://msdn.microsoft.com/en-us/library/23bk23zc(v=vs.100).aspx
        public override object InitializeLifetimeService() { return null; }

        [NotNull]
        private const string CashRegisterName = "FP700(plugin)";

        public SampleCashRegisterFactory()
        {
            DefaultDeviceSettings = InitDefaultDeviceSettings();
        }

        [NotNull]
        public string CodeName => CashRegisterName;

        [NotNull]
        public DeviceSettings DefaultDeviceSettings { get; }

        //Инициализировать настройки фискального регистратора
        private CashRegisterSettings InitDefaultDeviceSettings()
        {
            return new CashRegisterSettings
            {
                Code = CodeName,
                Settings = new List<DeviceSetting>(
                    typeof(SampleCashRegisterSettings).GetFields(BindingFlags.Static | BindingFlags.Public).Select(info => (DeviceSetting)info.GetValue(null))),
                Font0Width = new DeviceNumberSetting
                {
                    Name = "Font0Width",
                    Value = 42,
                    Label = "Символов в строке",
                    MaxValue = 100,
                    MinValue = 10,
                    SettingKind = DeviceNumberSettingKind.Integer
                },
                FiscalRegisterPaymentTypes = new List<FiscalRegisterPaymentType>
                {
                    //Заполнить таблицу типов оплат
                    new FiscalRegisterPaymentType
                    {
                        Id = "1",
                        Name = "Card"
                    },
                    new FiscalRegisterPaymentType
                    {
                        Id = "2",
                        Name = "Cash"
                    },
                    new FiscalRegisterPaymentType
                    {
                        Id = "3",
                        Name = "Credit"
                    },
                    new FiscalRegisterPaymentType
                    {
                        Id = "4",
                        Name = "Tare"
                    },
                },
                OfdProtocolVersion = new DeviceCustomEnumSetting
                {
                    Name = "OfdProtocolVersion",
                    Label = "Версия ФФД",
                    Values = new List<DeviceCustomEnumSettingValue>
                    {
                        new DeviceCustomEnumSettingValue
                        {
                            Name = string.Empty,
                            IsDefault = true,
                            Label = "Без ФФД"
                        }
                    }
                }
            };
        }

        public ICashRegister Create(Guid deviceId, [NotNull] CashRegisterSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            var sampleCashRegister = new SampleCashRegister(deviceId, settings);
            return sampleCashRegister;
        }
    }
}
