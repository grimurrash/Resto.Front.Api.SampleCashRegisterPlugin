using KasaGE;
using KasaGE.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Resto.Front.Api.Data.Device.Tasks;
using Resto.Front.Api.Data.Print;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Resto.Front.Api.SampleCashRegisterPlugin
{
    class FRgeorgia
    {
        Dp25 ecr;
       
        private bool execute = false;
        public bool isOpenFiscal = false;
        public bool isOpenNonFiscal = false;

        string _lastError = "";
       
        string LastError
        {
            get { return _lastError; }
            set { _lastError = string.IsNullOrWhiteSpace(value) ? "" : value.Trim(); }
        }

        readonly string comPort = "";

        readonly List<PrintTasks> tasks = new List<PrintTasks>();
        public FRgeorgia(string comPort)
        {
            try
            {
                ecr = new Dp25(comPort);
                this.comPort = comPort;
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public void OpenNonFiscal()
        {
            if (isOpenNonFiscal)
            {
                return;
            }

            PluginContext.Log.InfoFormat("Start openNonFiscal");
            LastError = ecr.OpenNonFiscalReceipt().ErrorCode;
            ExMessage("OpenNonFiscalReceipt");
            isOpenNonFiscal = true;
        }
        public void OpenFiscal(string session, string waiter, ReceiptType receiptType)
        {
            if (isOpenFiscal)
            {
                return;
            }

            PluginContext.Log.InfoFormat("Start openFiscal");
            LastError = ecr.OpenFiscalReceipt(session, waiter, receiptType).ErrorCode;

            if (!string.IsNullOrWhiteSpace(LastError))
            {
                if (LastError == "-111015")
                {
                    LastError = ecr.Total(PaymentMode.Cash).ErrorCode;
                    ExMessage("Total");
                    LastError = ecr.CloseFiscalReceipt().ErrorCode;
                    ExMessage("CloseFiscalReceipt");
                }
                LastError = ecr.OpenFiscalReceipt(session, waiter, receiptType).ErrorCode;
            }

            ExMessage("OpenFiscalReceipt");
            isOpenFiscal = true;
        }

        public void OpenDrawler()
        {
            PluginContext.Log.InfoFormat("Start openDrawler");
            LastError = ecr.OpenDrawer(10).ErrorCode;
            ExMessage("OpenDrawer");
        }

        public void InCash(decimal amount)
        {
            try
            {
                PluginContext.Log.InfoFormat("Start InCash");
                LastError = ecr.CashInCashOutOperation(Cash.In, amount).ErrorCode;
                ExMessage("CashInCashOutOperation InCash");
            }
            catch (KasaGE.Core.FiscalIOException ex)
            {
                PluginContext.Log.WarnFormat("inCash error: {0}", ex.Message);
                ecr = new Dp25(this.comPort);
            }
        }

        public void OutCash(decimal amount)
        {
            try
            {
                PluginContext.Log.InfoFormat("Start OutCash");
                LastError = ecr.CashInCashOutOperation(Cash.Out, amount).ErrorCode;
                ExMessage("CashInCashOutOperation OutCash");
            }
            catch (KasaGE.Core.FiscalIOException ex)
            {
                PluginContext.Log.WarnFormat("outCash error: {0}", ex.Message);
                ecr = new Dp25(this.comPort);
            }
        }

        private void ExMessage(string errorFunction = "")
        {
            if (string.IsNullOrWhiteSpace(LastError))
            {
                return;
            }
            
            PluginContext.Log.ErrorFormat("ERROR {0}: \"{1}\"", errorFunction, LastError);

            if (LastError == "-111050")
            {
                throw new Exception("Critical error when printing the receipt, please contact the plugin developer.");
            }

            if (LastError == "-111065")
            {
                throw new Exception("It was not possible to print the receipt, the amount in the receipt is 0, try to print the receipt again, in case of a repeat error, restart the terminal and the fiscal device.");
            }

            LastError = "";
        }

        /// <summary>
        /// Заменяет <qrcode>...</qrcode> на команду FP-700 для печати QR-кода.
        /// </summary>
        /// <param name="text">Исходный текст с тегами</param>
        /// <param name="moduleSize">Размер модуля (1-16)</param>
        /// <param name="errorCorrection">Уровень коррекции (L, M, Q, H)</param>
        /// <returns>Текст с заменёнными QR-кодами</returns>
        public static string ReplaceQRCodes(string text, int moduleSize = 4, string errorCorrection = "M")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            // ESC (0x1B) — управляющий символ для команд FP-700
            const string esc = "\x1B";

            return Regex.Replace(text, @"<qrcode>(.*?)</qrcode>", match =>
            {
                string data = match.Groups[1].Value.Trim();
                // Формируем команду печати QR-кода
                string command = $"{esc}ZQRCODE;{moduleSize};{errorCorrection};{data}\r\n";
                return command;
            }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        public List<string> ConvertDocumentStringToArray(string documentString)
        {
            List<string> resultList = new List<string>();
          
            foreach (var s in documentString.Split('\n'))
            {
                var trimText = s.TrimEnd();

                if (trimText.StartsWith("  <"))
                {
                    trimText = trimText.Substring(2);
                }

                resultList.Add(trimText);
            }

            return resultList;
        }
        
        private string SaleNameFormatter(string name)
        {
            name = Regex.Replace(name, @"\s+", " ");
            return name.Trim();
        }

        public string ConvertDocumentToString(Document document)
        {
            string text = document.Markup.ToString();

            text = text.Replace("\r", "");
            text = text.Replace("<doc>", "");
            text = text.Replace("</doc>", "");
            text = text.Replace("<doc />", "");
            text = text.Replace("<bell />", "");
            text = text.Replace("  <f0>", "<f0>");
            text = text.Replace("  <f1>", "<f1>");
            text = text.Replace("  <f2>", "<f2>");
            text = text.Replace("<f0>", "");
            text = text.Replace("<f1>", "");
            text = text.Replace("<f2>", "");
            text = text.Replace("</f0>", "");
            text = text.Replace("</f1>", "");
            text = text.Replace("</f2>", "");
            text = text.Replace("<f0 />", "");
            text = text.Replace("<f1 />", "");
            text = text.Replace("<f2 />", "");
            text = text.Replace("<papercut />", "");
            text = text.Replace("<pagecut />", "");
            text = ReplaceQRCodes(text);

            return text;
        }

        public void AddTextFiscal(Document document)
        {
            string text = ConvertDocumentToString(document);

            if (string.IsNullOrEmpty(text)) {  
                return; 
            }

            if (isOpenFiscal == true)
            {
                PluginContext.Log.InfoFormat("Start AddTextFiscal: text=\"{0}\"", document.Markup.ToString());


                foreach (var trimText in ConvertDocumentStringToArray(text))
                {
                    PluginContext.Log.InfoFormat("AddTextFiscal: s=\"{0}\"", trimText);
                    LastError = ecr.AddTextToFiscalReceipt(trimText).ErrorCode;
                }
                
                ExMessage("AddTextToFiscalReceipt");
            }
        }


        public void AddTask(ChequeTask chequeTask)
        {
            PluginContext.Log.InfoFormat("Add fiscal Task");
            PrintTasks task = new PrintTasks("fiscal", chequeTask);
            tasks.Add(task);
        }

        public void AddTask(Document document)
        {
            PluginContext.Log.InfoFormat("Add nofiscal Task");
            PrintTasks task = new PrintTasks("nofiscal", document);
            tasks.Add(task);
        }

        private void ExecuteFiscalTask(ChequeTask chequeTask, bool isReplay)
        {
            if (chequeTask.CardPayments.Count > 0)
            {
                return;
            }

            if (!isReplay && (isOpenFiscal || isOpenNonFiscal))
            {
                this.AddTask(chequeTask);
                return;
            }

            try
            {
                var recT = ReceiptType.Sale;
                if (chequeTask.IsRefund)
                {
                    recT = ReceiptType.Return;
                }

                PluginContext.Log.InfoFormat("Device: Cheque printed. {0} (105 class)", chequeTask.OrderId);
                OpenFiscal("007", "7", recT);

                PluginContext.Log.InfoFormat("Cheque Task {0}", JsonConvert.SerializeObject(chequeTask));
                
                bool isRound = false;
                if (chequeTask.RoundSum != null && chequeTask.RoundSum != 0)
                {
                    isRound = true;
                }

                AddTextFiscal(chequeTask.TextAfterCheque);
                   
                PluginContext.Log.InfoFormat("Start print sales");

                foreach (ChequeSale sale in chequeTask.Sales.OrderBy(s => s.Discount))
                {
                    //decimal price = sale.Sum ?? 0;
                    decimal price = sale.Price ?? 0;
                    decimal amount = sale.Amount ?? 1;
                    decimal discountPercent = sale.Discount ?? 0;
                    decimal discountSum = sale.DiscountSum ?? 0;
                    decimal increasetPercent = sale.Increase ?? 0;
                    decimal increaseSum = sale.IncreaseSum ?? 0;

                    if (price == 0 && discountPercent == 0 && discountSum == 0)
                    {
                        continue;
                    }

                    if (price > 0)
                    {
                        //if (amount > 0)
                        //{
                        //    price /= amount;
                        //}

                        if (isRound)
                        {
                            price = Math.Round(price);
                        }
                        else
                        {
                            price = Math.Round(price, 2);
                        }
                    }

                    if (sale.Discount > 0)
                    {
                        AddItemFiscalWithDiscount(sale.Name, price, amount, DiscountType.DiscountByPercentage, sale.Discount ?? 0);
                    }
                    else if (sale.DiscountSum > 0 && sale.Discount == 0)
                    {
                        AddItemFiscalWithDiscount(sale.Name, price, amount, DiscountType.DiscountBySum, sale.DiscountSum ?? 0);
                    }
                    else if (sale.Increase > 0)
                    {
                        AddItemFiscalWithDiscount(sale.Name, price, amount, DiscountType.SurchargeByPercentage, sale.Increase ?? 0);
                    }
                    else if (sale.IncreaseSum > 0 && sale.Increase == 0)
                    {
                        AddItemFiscalWithDiscount(sale.Name, price, amount, DiscountType.SurchargeBySum, sale.IncreaseSum ?? 0);
                    }
                    else
                    {
                        AddItemFiscalNotDiscount(sale.Name, price, amount);
                    }
                }
                   
                PluginContext.Log.InfoFormat("End print sales");

                AddTextFiscal(chequeTask.TextBeforeCheque);

                var isSuccess = this.PrintFiscal(PaymentMode.Cash);

                if (!isSuccess)
                {
                    if (!isReplay)
                    {
                        PluginContext.Log.WarnFormat("Replay print cheque");
                        ExecuteFiscalTask(chequeTask, true);
                        return;
                    } else
                    {
                        throw new Exception("Error print fiscal cheque");
                    }
                }

                PluginContext.Log.InfoFormat("Device: Cheque printed. {0} (176)", chequeTask.OrderId);
            }
            catch (Exception ex)
            {
                if (isReplay)
                {
                    throw ex;
                }

                if (isOpenFiscal)
                {
                    this.VoidOpenFiscalReceipt();
                }
                isOpenFiscal = false;
                PluginContext.Log.ErrorFormat("ExecuteFiscalTask ERROR: {0}\nTrace:\n{1}", ex.Message, ex.StackTrace);
                throw ex;
            }
        }

        private void ExecuteNotFiscalTask(Document document)
        {
            if (isOpenNonFiscal || isOpenNonFiscal)
            {
                AddTask(document);
                return;
            }

            try
            {
                string text = ConvertDocumentToString(document);

                PluginContext.Log.InfoFormat("Device: printed text (283):\n{0}", text);
                this.OpenNonFiscal();

                var resultText = "";
                foreach (var s in ConvertDocumentStringToArray(text))
                {
                    this.PrintTextNonFiscal(s);
                    resultText += s + "\n";
                }

                this.PrintNonFiscal();

                PluginContext.Log.InfoFormat("Device: printed text (283):\n{0} ", resultText);
            }
            catch (Exception ex)
            {
                isOpenNonFiscal = false;
                PluginContext.Log.WarnFormat("ExecuteNotFiscalTask ERROR: {0} details {1}", ex.Message, ex.InnerException.Message);
                throw ex;
            }
        }

        public void ExecuteTask()
        {
            if (execute==true)
            {
                return;
            }
            execute = true;

            PluginContext.Log.InfoFormat("Start ExecuteTask");
            PluginContext.Log.InfoFormat("Task count {0}", tasks.Count);

            var sTasks = tasks.ToArray();
            foreach (var task in sTasks)
            {
                try
                {
                    if (task.type == "fiscal")
                    {
                        ExecuteFiscalTask(task.ChequeTask, false);
                    }
                    else
                    {
                        ExecuteNotFiscalTask(task.document);
                    }
                    tasks.Remove(task);
                } catch (Exception e)
                {
                    tasks.Remove(task);
                    execute = false;
                    throw e;
                }
            }
            execute = false;

            if (tasks.Count > 0)
            {
                ExecuteTask();
            }
        }

        public void AddItemFiscalNotDiscount(string nameItem, decimal price, decimal amount)
        {
            nameItem = SaleNameFormatter(nameItem);

            PluginContext.Log.InfoFormat("AddItemFiscalNotDiscount: nameItem={0} | price={1} | amount={2}", nameItem, price, amount);
            LastError = ecr.RegisterSale(nameItem, price, amount, 1).ErrorCode;
            ExMessage("RegisterSale");
        }

        public void AddItemFiscalWithDiscount(string nameItem, decimal price, decimal amount, DiscountType discountType, decimal discountValue)
        {
            nameItem = SaleNameFormatter(nameItem);
            discountValue = Math.Round(discountValue, 2);

            PluginContext.Log.InfoFormat("AddItemFiscalWithDiscount: nameItem={0} | price={1} | amount={2} | discountType={3} discount={4}", nameItem, price, amount, discountType, discountValue);
            LastError = ecr.RegisterSale(nameItem, price, amount, 1, discountType, discountValue).ErrorCode;
            ExMessage("RegisterSale");
        }

        public void PrintTextNonFiscal(string text)
        {
            if (!isOpenNonFiscal)
            {
                return;
            }

            PluginContext.Log.InfoFormat("PrintTextNonFiscal:  text={0}", text);
            LastError = ecr.AddTextToNonFiscalReceipt(text).ErrorCode;
            ExMessage("AddTextToNonFiscalReceipt");
        }

        public void PrintNonFiscal()
        {
            if (!isOpenNonFiscal)
            {
                return;
            }

            PluginContext.Log.InfoFormat("PrintNonFiscal");
            LastError = ecr.CloseNonFiscalReceipt().ErrorCode;
            isOpenNonFiscal = false;
            return;
        }

        public void VoidOpenFiscalReceipt()
        {
            PluginContext.Log.WarnFormat("VoidOpenFiscalReceipt start");
            try
            {
                LastError = ecr.VoidOpenFiscalReceipt().ErrorCode;
                ExMessage("VoidOpenFiscalReceipt");
            }
            catch (Exception e)
            {
                PluginContext.Log.WarnFormat("ERROR VoidOpenFiscalReceipt {0} \n{1}", e.Message, e.StackTrace);
            }

            PluginContext.Log.WarnFormat("VoidOpenFiscalReceipt finished.");
        }

        public bool PrintFiscal(PaymentMode PM = PaymentMode.Cash)
        {
            if (!isOpenFiscal)
            {
                return false;
            }

            Thread.Sleep(1000);
            PluginContext.Log.InfoFormat("PrintFiscal");

            LastError = ecr.Total(PM).ErrorCode;
            ExMessage("Total");

            LastError = ecr.CloseFiscalReceipt().ErrorCode;
            var isSuccess = string.IsNullOrEmpty(LastError);
            ExMessage("CloseFiscalReceipt");

            isOpenFiscal = false;
            return isSuccess;
        }

        public void PrintReport(ReportType typeReport)
        {
            try
            {
                PluginContext.Log.InfoFormat("PrintReport");
                LastError = ecr.PrintReport(typeReport).ErrorCode;
                LastError = "";
            }
            catch (Exception ex)
            {
                PluginContext.Log.WarnFormat("printReport ERROR: {2} details {3}", ex.Message, ex.InnerException.Message);
                ecr = new Dp25(comPort);
                LastError = ecr.PrintReport(typeReport).ErrorCode;
                LastError = "";
            }
        }
        public void Dispose()
        {
            ecr.Dispose();
        }
    }
}
