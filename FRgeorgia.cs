using System;
using System.Collections.Generic;
using System.Linq;
using KasaGE;
using KasaGE.Commands;
using Newtonsoft.Json;
using Resto.Front.Api.Data.Device.Tasks;

namespace Resto.Front.Api.SampleCashRegisterPlugin
{
    class FRgeorgia
    {
        Dp25 ecr;
        public bool isOpenFiscal = false;
        public bool isOpenNonFiscal = false;
        string lastError = "";
        readonly string comPort = "";
        bool execute = false;
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
            if (isOpenNonFiscal == false)
            {
                PluginContext.Log.InfoFormat("Start openNonFiscal");
                lastError = ecr.OpenNonFiscalReceipt().ErrorCode;
                ExMessage("OpenNonFiscalReceipt");
                isOpenNonFiscal = true;
            }
        }
        public void OpenFiscal(string session, string waiter, ReceiptType receiptType)
        {
            if (isOpenFiscal == false)
            {
                PluginContext.Log.InfoFormat("Start openFiscal");
                lastError = ecr.OpenFiscalReceipt(session, waiter, receiptType).ErrorCode;
                ExMessage("OpenFiscalReceipt");
                isOpenFiscal = true;
            }
        }

        public void OpenDrawler()
        {
            PluginContext.Log.InfoFormat("Start openNonFiscal");
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
            lastError = "";

            if (lastError == "-111015")
            {
                throw new Exception("გავიდა 24 საათი");
            }
        }

        public void AddTextFiscal(string text)
        {
            if (string.IsNullOrEmpty(text)) {  
                return; 
            }

            if (isOpenFiscal == true)
            {
                PluginContext.Log.InfoFormat("Start AddTextFiscal: text=\"{0}\"", text); 
                text = text.Replace("\r", "");
                
                foreach (var s in text.Split('\n'))
                {
                    PluginContext.Log.InfoFormat("AddTextFiscal: s=\"{0}\"", s);
                    lastError = ecr.AddTextToFiscalReceipt(s).ErrorCode;
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

        public void AddTask(string text)
        {
            PluginContext.Log.InfoFormat("Add nofiscal Task");
            PrintTasks task = new PrintTasks("nofiscal", text);
            tasks.Add(task);
        }

        public void ExecuteTask()
        {
            if (execute==true)
            {
                return;
            }
            PluginContext.Log.InfoFormat("Start ExecuteTask");
            execute = true;
            PrintTasks Task;

            for (int j = 0; j < tasks.Count; j++)
            {
                try
                {
                    Task = tasks.ElementAt(j);
                    if (Task == null)
                    {
                        return;
                    }
                }
                catch (Exception)
                {
                    return;
                }

                if (Task.type == "fiscal")
                {
                    try
                    {
                        ChequeTask chequeTask = Task.ChequeTask;
                        bool card = false;
                        foreach (var type in chequeTask.CardPayments)
                        {
                            PluginContext.Log.InfoFormat("CardPayments register id {0} (102 class)", type.PaymentRegisterId);
                            card = true;
                        }
                        PluginContext.Log.InfoFormat("Device: Cheque printed. (105 class)");
                        var recT = ReceiptType.Sale;
                        if (chequeTask.IsRefund && card == false)
                        {
                            recT = ReceiptType.Return;
                        }
                        if (card == false)
                        {
                            PluginContext.Log.InfoFormat("Sales {0}", JsonConvert.SerializeObject(chequeTask));
                            bool isRound = false;
                            if (chequeTask.RoundSum != null && chequeTask.RoundSum != 0)
                            {
                                isRound = true;
                            }

                            OpenFiscal("007", "7", recT);
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
                            PrintFiscal();
                        }
                        PluginContext.Log.InfoFormat("Device: Cheque printed. (143 class)");
                    }
                    catch (Exception ex)
                    {
                        PluginContext.Log.WarnFormat("Device:: {2} (287) details {3}", ex.Message, ex.InnerException.Message);
                        throw new Exception(ex.Message);
                    }

                }
                else
                {
                    try
                    {
                        string text = Task.text;
                        this.OpenNonFiscal();
                        text = text.Replace("<bell/>", "");
                        text = text.Replace("<f0/>", "");
                        text = text.Replace("<f1/>", "");
                        text = text.Replace("<f2/>", "");
                        text = text.Replace("<papercut/>", "");
                        string[] mas = text.Split('\n');
                        foreach (var s in mas)
                        {
                            this.PrintTextNonFiscal(s);
                        }
                        this.PrintNonFiscal();


                        PluginContext.Log.InfoFormat("Device:  printed text {2} (283)", text);
                    }
                    catch (Exception ex)
                    {
                        PluginContext.Log.WarnFormat("Device:: {2} (287) details {3}", ex.Message, ex.InnerException.Message);
                        throw new Exception(ex.Message);
                    }
                }
            }
            tasks.Clear();
            execute = false;
            return;
        }

        public void AddItemFiscalNotDiscount(string nameItem, decimal price, decimal amount)
        {
            if (isOpenFiscal || isOpenNonFiscal)
            {
                PluginContext.Log.InfoFormat("AddItemFiscalNotDiscount: nameItem={0} | price={1} | amount={2}", nameItem, price, amount);
                lastError = ecr.RegisterSale(nameItem, price, amount, 1).ErrorCode;
                ExMessage("RegisterSale");
            }
        }

        public void AddItemFiscalWithDiscount(string nameItem, decimal price, decimal amount, DiscountType discountType, decimal discountValue)
        {
            if (isOpenFiscal || isOpenNonFiscal)
            {
                PluginContext.Log.InfoFormat("AddItemFiscalWithDiscount: nameItem={0} | price={1} | amount={2} | discountType={3} discount={4}", nameItem, price, amount, discountType, discountValue);
                lastError = ecr.RegisterSale(nameItem, price, amount, 1, discountType, discountValue).ErrorCode;
                ExMessage("RegisterSale");
            }
        }

        public void PrintTextNonFiscal(string text)
        {
            if (isOpenNonFiscal)
            {
                PluginContext.Log.InfoFormat("PrintTextNonFiscal:  text={0}", text);
                lastError = ecr.AddTextToNonFiscalReceipt(text).ErrorCode;
                ExMessage("AddTextToNonFiscalReceipt");
            }
        }
        public void PrintNonFiscal()
        {
            if (isOpenNonFiscal)
            {
                PluginContext.Log.InfoFormat("PrintNonFiscal"); 
                lastError = ecr.CloseNonFiscalReceipt().ErrorCode;
                ExMessage("CloseNonFiscalReceipt");
                isOpenNonFiscal = false;
                this.ExecuteTask();
            }
        }
        public void PrintFiscal(PaymentMode PM = PaymentMode.Cash)
        {
            if (isOpenFiscal)
            {
                PluginContext.Log.InfoFormat("PrintFiscal"); 
                lastError = ecr.Total(PM).ErrorCode;
                ExMessage("Total");
                lastError = ecr.CloseFiscalReceipt().ErrorCode;
                isOpenFiscal = false;
                ExMessage("CloseFiscalReceipt");
                this.ExecuteTask();
            }
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
