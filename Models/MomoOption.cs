namespace VelvySkinWeb.Models
{
    public class MomoOption
    {
        public string EndPoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/create";
        public string PartnerCode { get; set; } = "MOMOBKUN20180529";
        public string AccessKey { get; set; } = "klm05TvNCzjfasSq";
        public string SecretKey { get; set; } = "b84qH6pBnfLw05YInT5Eosv2B6Bng4B9";
    }
}