using Resto.Front.Api.Data.Device.Tasks;
using Resto.Front.Api.Data.Print;


namespace Resto.Front.Api.SampleCashRegisterPlugin
{
    class PrintTasks
    {
        public string type = "";
        public ChequeTask ChequeTask;
        public Document document;
        public PrintTasks (string type,ChequeTask chequeTask)
        {
            this.type = type;
            this.ChequeTask = chequeTask;
        }
        public PrintTasks(string type, Document document)
        {
            this.type = type;
            this.document = document;
        }
    }
}
