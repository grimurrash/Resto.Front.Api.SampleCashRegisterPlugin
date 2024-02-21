using Resto.Front.Api.Data.Device.Tasks;


namespace Resto.Front.Api.SampleCashRegisterPlugin
{
    class PrintTasks
    {
        public string type = "";
        public ChequeTask ChequeTask;
        public string text = "";
        public PrintTasks (string type,ChequeTask chequeTask)
        {
            this.type = type;
            this.ChequeTask = chequeTask;
        }
        public PrintTasks(string type, string text)
        {
            this.type = type;
            this.text = text;
        }
    }
}
