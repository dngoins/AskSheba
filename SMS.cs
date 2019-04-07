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

        private string _accountSid = "";
        private string _authToken = "";

        public SMS()
        {
            TwilioClient.Init(_accountSid, _authToken);
        }

        public void SendMessage(string phoneNumber, string smsMessage)
        {
            var messageOptions = new CreateMessageOptions(new PhoneNumber(string.Format("+1{0}", phoneNumber)));
            messageOptions.From = new PhoneNumber("");
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
