using KasaGE;
using KasaGE.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Resto.Front.Api.Data.Device.Tasks;
using Resto.Front.Api.Data.Print;
using System;
using System.Collections.Generic;
using System.Linq;
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


        string lastError = "";
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
            lastError = ecr.OpenNonFiscalReceipt().ErrorCode;
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
            lastError = ecr.OpenFiscalReceipt(session, waiter, receiptType).ErrorCode;

            if (!string.IsNullOrWhiteSpace(lastError) && lastError.TrimEnd('\t') == "-111016")
            {
                lastError = ecr.VoidOpenFiscalReceipt().ErrorCode;
                ExMessage("OpenFiscalReceipt");
                lastError = ecr.OpenFiscalReceipt(session, waiter, receiptType).ErrorCode;
            }

            ExMessage("OpenFiscalReceipt");
            isOpenFiscal = true;
        }

        public void OpenDrawler()
        {
            PluginContext.Log.InfoFormat("Start openDrawler");
            lastError = ecr.OpenDrawer(10).ErrorCode;
            ExMessage("OpenDrawer");
        }

        public void InCash(decimal amount)
        {
            try
            {
                PluginContext.Log.InfoFormat("Start InCash");
                lastError = ecr.CashInCashOutOperation(Cash.In, amount).ErrorCode;
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
                lastError = ecr.CashInCashOutOperation(Cash.Out, amount).ErrorCode;
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
            if (string.IsNullOrWhiteSpace(lastError))
            {
                return;
            }

            PluginContext.Log.ErrorFormat("ERROR {0}: {1}", errorFunction, lastError);

            //if (lastError == "-111015\t")
            //{
            //    throw new Exception("გავიდა 24 საათი");
            //}

            lastError = "";
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
            if (string.IsNullOrEmpty(text))
                return text;

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
        
        public string ConvertDocumentToString(Data.Print.Document document)
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
                    lastError = ecr.AddTextToFiscalReceipt(trimText).ErrorCode;
                }
                
                ExMessage("AddTextToFiscalReceipt");
            }
        }
        public string GetLastError()
        {
            var error = lastError;
            lastError = "";
            return error;
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

            if (isOpenFiscal == true)
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
                   
                var printTry = 0;
                var isSuccess = false;
                do
                {
                    printTry++;
                    isSuccess = this.PrintFiscal(PaymentMode.Cash, printTry);
                } while (printTry < 3 && isSuccess == false);

                if (!isSuccess)
                {
                    if (!isReplay)
                    {
                        PluginContext.Log.WarnFormat("Replay print cheque");
                        ExecuteFiscalTask(chequeTask, true);
                        return;
                    } else
                    {
                        throw new Exception("Error close fiscal printer");
                    }
                }

                PluginContext.Log.InfoFormat("Device: Cheque printed. {0} (176)", chequeTask.OrderId);
            }
            catch (Exception ex)
            {
                PluginContext.Log.WarnFormat("Device: Error {0} (287}", ex.Message);
                PluginContext.Log.WarnFormat("Device: StackTrace {0} (287)", ex.StackTrace);
                throw new Exception(ex.Message);
            }
        }

        private void ExecuteNotFiscalTask(Document document)
        {
            if (isOpenNonFiscal == true)
            {
                AddTask(document);
                return;
            }

            try
            {
                string text = ConvertDocumentToString(document);

                PluginContext.Log.InfoFormat("Device: printed text {2} (283)", text);
                this.OpenNonFiscal();

                var resultText = "";
                foreach (var s in ConvertDocumentStringToArray(text))
                {
                    this.PrintTextNonFiscal(s);
                    resultText += s + "\n";
                }

                var printTry = 0;
                var isSuccess = false;
                do
                {
                    printTry++;
                    isSuccess = this.PrintNonFiscal(printTry);
                } while (printTry < 3 && isSuccess == false);

                if (!isSuccess)
                {
                    throw new Exception("Error close not fiscal print");
                }

                PluginContext.Log.InfoFormat("Device: printed text {2} (283)", resultText);
            }
            catch (Exception ex)
            {
                PluginContext.Log.WarnFormat("Device:: {2} (287) details {3}", ex.Message, ex.InnerException.Message);
                throw new Exception(ex.Message);
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

            var sTasks = new List<PrintTasks>(tasks);
            tasks.Clear();
            PluginContext.Log.InfoFormat("Task count {0}", sTasks.Count);

            foreach (var task in sTasks)
            {
                if (task.type == "fiscal")
                {
                    ExecuteFiscalTask(task.ChequeTask, false);
                }
                else
                {
                    ExecuteNotFiscalTask(task.document);
                }
            }
            sTasks.Clear();
            execute = false;

            if (tasks.Count > 0)
            {
                ExecuteTask();
            }
        }

        public void AddItemFiscalNotDiscount(string nameItem, decimal price, decimal amount)
        {
            PluginContext.Log.InfoFormat("AddItemFiscalNotDiscount: nameItem={0} | price={1} | amount={2}", nameItem, price, amount);
            lastError = ecr.RegisterSale(nameItem, price, amount, 1).ErrorCode;
            ExMessage("RegisterSale");
            Thread.Sleep(200);
        }

        public void AddItemFiscalWithDiscount(string nameItem, decimal price, decimal amount, DiscountType discountType, decimal discountValue)
        { 
            discountValue = Math.Round(discountValue, 2);

            PluginContext.Log.InfoFormat("AddItemFiscalWithDiscount: nameItem={0} | price={1} | amount={2} | discountType={3} discount={4}", nameItem, price, amount, discountType, discountValue);
            lastError = ecr.RegisterSale(nameItem, price, amount, 1, discountType, discountValue).ErrorCode;
            ExMessage("RegisterSale");
            Thread.Sleep(200);
        }

        public void PrintTextNonFiscal(string text)
        {
            if (!isOpenNonFiscal)
            {
                return;
            }

            PluginContext.Log.InfoFormat("PrintTextNonFiscal:  text={0}", text);
            lastError = ecr.AddTextToNonFiscalReceipt(text).ErrorCode;
            ExMessage("AddTextToNonFiscalReceipt");
        }

        public bool PrintNonFiscal(int printTry)
        {
            if (!isOpenNonFiscal)
            {
                OpenNonFiscal();
                return false;
            }

            PluginContext.Log.InfoFormat("PrintNonFiscal " + printTry);
            lastError = ecr.CloseNonFiscalReceipt().ErrorCode;

            if (!string.IsNullOrEmpty(lastError))
            {
                ExMessage("CloseNonFiscalReceipt " + printTry);
                return false;
            }
            
            isOpenNonFiscal = false;
            return true;
        }

        public bool PrintFiscal(PaymentMode PM = PaymentMode.Cash, int printTry = 1)
        {
            if (!isOpenFiscal)
            {
                return false;
            }

            PluginContext.Log.InfoFormat("PrintFiscal"); 
            lastError = ecr.Total(PM).ErrorCode;
            ExMessage("Total");
            lastError = ecr.CloseFiscalReceipt().ErrorCode;

            if (!string.IsNullOrWhiteSpace(lastError) && lastError.TrimEnd('\t') == "-111065")
            {
                ExMessage("CloseFiscalReceipt " + printTry);
                lastError = ecr.VoidOpenFiscalReceipt().ErrorCode;
                ExMessage("VoidOpenFiscalReceipt " + printTry);
                lastError = ecr.CloseFiscalReceipt().ErrorCode;
            }

            ExMessage("CloseFiscalReceipt " + printTry);
            isOpenFiscal = false;
            Thread.Sleep(1000);
            return true;
        }

        public void PrintReport(ReportType typeReport)
        {
            try
            {
                PluginContext.Log.InfoFormat("PrintReport");
                lastError = ecr.PrintReport(typeReport).ErrorCode;
                lastError = "";
            }
            catch (Exception ex)
            {
                PluginContext.Log.WarnFormat("printReport ERROR: {2} details {3}", ex.Message, ex.InnerException.Message);
                ecr = new Dp25(comPort);
                lastError = ecr.PrintReport(typeReport).ErrorCode;
                lastError = "";
            }
        }
        public void Dispose()
        {
            ecr.Dispose();
        }
    }
}
