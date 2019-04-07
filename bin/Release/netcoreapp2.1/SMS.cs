using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace CoreBot
{
    public class SMS
    {

        private string _accountSid = "AC354c6bd3c1b5eef3c9641f9b3838f273";
        private string _authToken = "05ac899b05461d928070509a79f47eba";

        public SMS()
        {
            TwilioClient.Init(_accountSid, _authToken);
        }

        public void SendMessage(string phoneNumber, string smsMessage)
        {
            var messageOptions = new CreateMessageOptions(new PhoneNumber(string.Format("+1{0}", phoneNumber)));
            messageOptions.From = new PhoneNumber("+19545194011");
            messageOptions.Body = smsMessage;
            try
            {
                var message = MessageResource.Create(messageOptions);
                var body = message.Body;

            }
            catch (Exception e)
            {

                
            }
            
        }
    }
}
