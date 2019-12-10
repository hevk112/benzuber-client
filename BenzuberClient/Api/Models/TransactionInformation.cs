using System;

namespace Benzuber.Api.Models
{
    public class TransactionInformation
    {
        public string TransactionID { get; set; }
        public string RequestID { get; set; }
        public DateTime DateTime { get; set; }
        public int StationID { get; set; }
        public int Pump { get; set; }
        public int FuelCode { get; set; }
        public decimal Amount { get; set; }
        public decimal FillingOverAmount { get; set; }
        public TransactionStates TransactionState { get; set; }
        public string StatusReason { get; set; }
        public string UserID { get; set; }
        public string Error { get; set; }
        public long PumRRN { get; set; }
        public enum TransactionStates
        {
            /// <summary>
            /// Заказ только что создан
            /// </summary>
            New,
            /// <summary>
            /// Заказ передан на ТРК
            /// </summary>
            OnPump,
            /// <summary>
            /// Осуществляется отпуск топлива
            /// </summary>
            Filling,
            /// <summary>
            /// Заказ завершен, необходимо подтверждение со стороны платежного сервера
            /// </summary>
            NeedCommit,
            /// <summary>
            /// Заказ завершен, фактически отпущенная сумма подтверждена
            /// </summary>
            Over,
            /// <summary>
            /// Заказ отменён
            /// </summary>
            Canceled,
            /// <summary>
            /// При выполнении заказа произошла ошибка
            /// </summary>
            Error,
            /// <summary>
            /// Ожидание снятия нуного пистолета на ТРК
            /// </summary>
            Wait_Nuzz,
            /// <summary>
            /// Ожидание подтверждение оплаты, заказа по пост-оплате, от системы управления.
            /// </summary>
            PostPaid
        }
    }
}
