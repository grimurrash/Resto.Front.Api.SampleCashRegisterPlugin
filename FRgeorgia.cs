using System;
using System.Collections.Generic;
using System.Linq;
using KasaGE;
using KasaGE.Commands;
using Resto.Front.Api;
using Resto.Front.Api.Data.Device.Tasks;

namespace Resto.Front.Api.SampleCashRegisterPlugin
{
    class FRgeorgia
    {
        Dp25 ecr;
        public bool isOpenFiscal = false;
        public bool isOpenNonFiscal = false;
        string lastError = "";
        string comPort = "";
        bool execute = false;
        List <PrintTasks> tasks = new List<PrintTasks>();
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
        public void openNonFiscal()
        {
            if (isOpenNonFiscal == false)
            {
                lastError = ecr.OpenNonFiscalReceipt().ErrorCode;
                PluginContext.Log.WarnFormat("ERROR CODE OpenNonFiscalReceipt: {0}", lastError);
                lastError = "";
                isOpenNonFiscal = true;
            }
        }
        public void openFiscal(string session, string waiter, ReceiptType receiptType)
        {
            if (isOpenFiscal == false)
            {
                lastError = ecr.OpenFiscalReceipt(session, waiter, receiptType).ErrorCode;
                PluginContext.Log.WarnFormat("ERROR CODE OpenFiscal: {0}", lastError);
                lastError = "";
                isOpenFiscal = true;
                exMessage();
            }
        }

        public void openDrawler()
        {
            lastError = ecr.OpenDrawer(10).ErrorCode;
            PluginContext.Log.WarnFormat("ERROR CODE openDrawler: {0}", lastError);
            lastError = "";
        }

        public void inCash(decimal amount)
        {
            try
            {
                lastError = ecr.CashInCashOutOperation(Cash.In, amount).ErrorCode;
                PluginContext.Log.WarnFormat("ERROR CODE inCash: {0}", lastError);
                lastError = "";
            }
            catch (KasaGE.Core.FiscalIOException ex)
            {
                PluginContext.Log.WarnFormat("inCash error: {0}", ex.Message);
                ecr = new Dp25(this.comPort);
            }
        }

        public void outCash(decimal amount)
        {
            try
            {
                lastError = ecr.CashInCashOutOperation(Cash.Out, amount).ErrorCode;
                PluginContext.Log.WarnFormat("ERROR CODE outCash: {0}", lastError);
                lastError = "";
                
            }
            catch (KasaGE.Core.FiscalIOException ex)
            {
                PluginContext.Log.WarnFormat("outCash error: {0}", ex.Message);
                ecr = new Dp25(this.comPort);
            }
        }
        private void exMessage()
        {
            if (lastError=="-111015")
            {
                PluginContext.Log.WarnFormat("ERROR CODE : {0}",lastError);
                lastError = "";
                throw new Exception("გავიდა 24 საათი");
            }
        }
        public void addTextFiscal (string text)
        {
            if (isOpenFiscal == true)
            {
                lastError =  ecr.AddTextToFiscalReceipt(text).ErrorCode;
                
                exMessage();
            }
        }
        public string getLastError()
        {
            var error = lastError;
            lastError = "";
            return error;
        }

        public void addTask(string type,ChequeTask chequeTask)
        {
            PrintTasks task = new PrintTasks("fiscal",chequeTask);
            tasks.Add(task);
        }

        public void addTask(string type, string text)
        {
            PrintTasks task = new PrintTasks("nofiscal", text);
            tasks.Add(task);
        }

        public void ExecuteTask()
        {
            if (execute==true)
            {
                return;
            }
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
                catch (Exception ex)
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
                            this.openFiscal("007", "7", recT);
                            this.addTextFiscal(chequeTask.TextAfterCheque);
                            for (int i = 0; i < chequeTask.Sales.Count; i++)
                            {
                                if (chequeTask.Sales.ElementAt(i).Price == null || chequeTask.Sales.ElementAt(i).Price == 0)
                                {
                                    continue;
                                }
                                decimal price = (decimal)chequeTask.Sales.ElementAt(i).Sum;
                                if (chequeTask.Sales.ElementAt(i).Amount != null)
                                {
                                    price = (decimal)chequeTask.Sales.ElementAt(i).Sum / (decimal)chequeTask.Sales.ElementAt(i).Amount;
                                }
                                /*if (chequeTask.Sales.ElementAt(i).DiscountSum != null && chequeTask.Sales.ElementAt(i).DiscountSum != 0)
                                {
                                    if (chequeTask.Sales.ElementAt(i).Amount != null)
                                    {
                                        price -= (decimal)chequeTask.Sales.ElementAt(i).DiscountSum / (decimal)chequeTask.Sales.ElementAt(i).Amount;
                                    }
                                    else
                                    {
                                        price -= (decimal)chequeTask.Sales.ElementAt(i).DiscountSum;
                                    }
                                }
                                if (chequeTask.Sales.ElementAt(i).IncreaseSum != null && chequeTask.Sales.ElementAt(i).IncreaseSum != 0)
                                {
                                    if (chequeTask.Sales.ElementAt(i).Amount != null)
                                    {
                                        price += (decimal)chequeTask.Sales.ElementAt(i).IncreaseSum / (decimal)chequeTask.Sales.ElementAt(i).Amount;
                                    }
                                    else
                                    {
                                        price += (decimal)chequeTask.Sales.ElementAt(i).IncreaseSum;
                                    }
                                }*/
                                if (chequeTask.RoundSum != null && chequeTask.RoundSum != 0)
                                {
                                    price = Math.Round(price);
                                }
                                else
                                {
                                    price = Math.Round(price, 2);
                                }
                                if (chequeTask.Sales.ElementAt(i).Amount != null)
                                {
                                    this.addItemFiscal(chequeTask.Sales.ElementAt(i).Name, price, (decimal)chequeTask.Sales.ElementAt(i).Amount);
                                }
                                else
                                {
                                    this.addItemFiscal(chequeTask.Sales.ElementAt(i).Name, price, 1.00M);
                                }
                            }
                            this.addTextFiscal(chequeTask.TextBeforeCheque);
                            this.printFiscal();
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
                        this.openNonFiscal();
                        text = text.Replace("<bell/>", "");
                        text = text.Replace("<f0/>", "");
                        text = text.Replace("<f1/>", "");
                        text = text.Replace("<f2/>", "");
                        text = text.Replace("<papercut/>", "");
                        string[] mas = text.Split('\n');
                        foreach (var s in mas)
                        {
                            this.printTextNonFiscal(s);
                        }
                        this.printNonFiscal();


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

        public void addItemFiscal(string nameItem, decimal price, decimal amount)
        {
            if (isOpenFiscal||isOpenNonFiscal)
            {
                lastError = ecr.RegisterSale(nameItem, price, amount, (int)TaxCode.A).ErrorCode;
                PluginContext.Log.WarnFormat("ERROR CODE AddItemFiscal: {0}", lastError);
                lastError = "";
                exMessage();
            }
        }
        public void printTextNonFiscal(string text)
        {
            if (isOpenNonFiscal)
            {
               lastError = ecr.AddTextToNonFiscalReceipt(text).ErrorCode;
                PluginContext.Log.WarnFormat("ERROR CODE AddTextToNonFiscalReceipt: {0}", lastError);
                lastError = "";
            }
        }
        public void printNonFiscal()
        {
            if (isOpenNonFiscal)
            {
                lastError = ecr.CloseNonFiscalReceipt().ErrorCode;
                PluginContext.Log.WarnFormat("ERROR CODE CloseNonFiscalReceipt: {0}", lastError);
                lastError = "";
                isOpenNonFiscal = false;
                this.ExecuteTask();
            }
        }
        public void printFiscal(PaymentMode PM = PaymentMode.Cash)
        {
            if (isOpenFiscal)
            {
                lastError = ecr.Total(PM).ErrorCode;
                PluginContext.Log.WarnFormat("ERROR CODE TOTAL: {0}", lastError);
                lastError = "";
                lastError = ecr.CloseFiscalReceipt().ErrorCode;
                isOpenFiscal = false;
                exMessage();
                PluginContext.Log.WarnFormat("ERROR CODE CloseFiscal: {0}", lastError);
                lastError = "";
                this.ExecuteTask();
            }
        }
        public void printReport(ReportType typeReport)
        {
            try
            {
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
