using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VelvySkinWeb.Services
{
    public class ZaloPayService
    {
        private readonly string appId = "2553";
        private readonly string key1 = "PcY4iZIKFCIdgZvA6ueMcMHHUbRLYjPL";
        private readonly string createOrderUrl = "https://sb-openapi.zalopay.vn/v2/create";

        public async Task<string> CreatePaymentUrl(int orderId, decimal amount, string hostUrl)
        {
            Random rnd = new Random();
            // ZaloPay bắt buộc mã đơn hàng phải có dạng yymmdd_xxxxxx
            string app_trans_id = DateTime.Now.ToString("yyMMdd") + "_" + orderId + "_" + rnd.Next(1000, 9999);
            string app_time = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            
            // Đường link ZaloPay sẽ đá về sau khi khách quét mã xong
            var embed_data = new { redirecturl = $"{hostUrl}/Cart/ZaloPayCallback" };
            var items = new[] { new { itemid = "1", itemname = "Thanh toan don hang Velvy", itemprice = amount, itemquantity = 1 } };

            var param = new Dictionary<string, string>
            {
                { "app_id", appId },
                { "app_user", "VelvySkin_User" },
                { "app_time", app_time },
                { "amount", amount.ToString("0") },
                { "app_trans_id", app_trans_id },
                { "embed_data", JsonConvert.SerializeObject(embed_data) },
                { "item", JsonConvert.SerializeObject(items) },
                { "description", $"Velvy Skin - Thanh toan don hang #{orderId}" },
                { "bank_code", "" } // Để trống thì nó sẽ ra trang chọn phương thức của ZaloPay
            };

            // THUẬT TOÁN MÃ HÓA HMAC-SHA256 THEO ĐÚNG CHUẨN ZALOPAY
            string data = appId + "|" + app_trans_id + "|" + param["app_user"] + "|" + param["amount"] + "|" + app_time + "|" + param["embed_data"] + "|" + param["item"];
            param.Add("mac", ComputeHmacSha256(data, key1));

            // GỌI API LÊN MÁY CHỦ
            using (var client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(param);
                var response = await client.PostAsync(createOrderUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();
                var responseData = JsonConvert.DeserializeObject<dynamic>(responseString);

                // return_code == 1 là tạo mã QR thành công
                if (responseData.return_code == 1)
                {
                    return responseData.order_url; 
                }
                return null;
            }
        }

        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(messageBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }
}