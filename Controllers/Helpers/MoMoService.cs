using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json; // Nhớ tải thư viện Newtonsoft.Json qua NuGet nha sếp

namespace VelvySkinWeb.Services
{
    public class MoMoService
    {

        private readonly string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
        private readonly string partnerCode = "MOMOBKUN20180529";
        private readonly string accessKey = "klm05TvNBzhg7h7j";
        private readonly string secretKey = "at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa";

        public async Task<string> CreatePaymentUrl(string orderId, string amount, string orderInfo)
        {
            string requestId = Guid.NewGuid().ToString();
            string redirectUrl = "https://localhost:7079/Order/MoMoReturn"; // Link web sếp để MoMo đá về sau khi quét xong
            string ipnUrl = "https://chuaco.ngrok.app/api/momo-webhook"; // Cứ để tạm, lát mình dùng Ngrok thay sau
            string requestType = "captureWallet";
            string extraData = "";


            string rawHash = "accessKey=" + accessKey +
                             "&amount=" + amount +
                             "&extraData=" + extraData +
                             "&ipnUrl=" + ipnUrl +
                             "&orderId=" + orderId +
                             "&orderInfo=" + orderInfo +
                             "&partnerCode=" + partnerCode +
                             "&redirectUrl=" + redirectUrl +
                             "&requestId=" + requestId +
                             "&requestType=" + requestType;


            string signature = ComputeHmacSha256(rawHash, secretKey);


            var message = new
            {
                partnerCode = partnerCode,
                partnerName = "Velvy Skin",
                storeId = "MomoTestStore",
                requestId = requestId,
                amount = amount,
                orderId = orderId,
                orderInfo = orderInfo,
                redirectUrl = redirectUrl,
                ipnUrl = ipnUrl,
                lang = "vi",
                requestType = requestType,
                extraData = extraData,
                signature = signature
            };

            using (HttpClient client = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(message), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(endpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();


                dynamic responseData = JsonConvert.DeserializeObject(responseString);
                return responseData.payUrl; 
            }
        }


        private string ComputeHmacSha256(string message, string secretKey)
        {
            byte[] keyByte = Encoding.UTF8.GetBytes(secretKey);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);
                string hex = BitConverter.ToString(hashmessage);
                return hex.Replace("-", "").ToLower();
            }
        }
    }
}