// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Builder.AI.QnA;
using CoreBot;
using System;
using Newtonsoft.Json.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Net.Http;
using Newtonsoft.Json;
using System.Data.Sql;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.BotBuilderSamples
{
    public class BasicBotState
    {
        public long Score;
        public string Data;
        public string Name;
        public string phoneNumber;
        public string ConfirmationKey;
        public string CSRNGKey;
        public string currentQuestion;
        public string currentAnswer;
        public string currentFact;
        public string previousQuestion;
        public string previousAnswer;
        public string previousFact;
        public bool UserVerified = false;
    }

    // This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
    // to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
    // each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
    // The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
    // and the requirement is that all BotState objects are saved at the end of a turn.
    public class DialogBot<T> : ActivityHandler where T : Dialog 
    {
        protected readonly Dialog _dialog;
        protected readonly BotState _conversationState;
        protected readonly BotState _userState;
        protected readonly ILogger _logger;
        private IConfiguration _configuration;
        private readonly IStatePropertyAccessor<DialogState> _dialogStateAccessor;
        private IStatePropertyAccessor<BasicBotState> BasicBotStateAccessor { get; }

        private const string WelcomeText = "This bot will help you to get started with QnA Maker. Type a query to get started.";

        private QnAMaker _qnaService;
        private DialogSet Dialogs { get; set; }

        public DialogBot(IConfiguration configuration, ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
        {
            _configuration = configuration;
            _conversationState = conversationState;
            _dialogStateAccessor = _conversationState.CreateProperty<DialogState>(nameof(DialogState));

            _userState = userState;
            _dialog = dialog;
            _logger = logger;
            BasicBotStateAccessor = _userState.CreateProperty<BasicBotState>("BasicBotState");

            this.InitServices();
        }

        private void InitServices()
        {
            var hostname =  _configuration["QnAEndpointHostName"];
            if (!hostname.StartsWith("https://"))
            {
                hostname = string.Concat("https://", hostname);
            }

            if (!hostname.EndsWith("/qnamaker"))
            {
                hostname = string.Concat(hostname, "/qnamaker");
            }

            this._qnaService = new QnAMaker(new QnAMakerEndpoint
            {
                KnowledgeBaseId =  _configuration["QnAKnowledgebaseId"],
            EndpointKey =  _configuration["QnAAuthKey"],
                Host = hostname
            });

            var joinCouncilSteps = new WaterfallStep[]
          {
                GetSacredCodeStepAsync,               
                ExplainGameStepAsync,
                PlayGameStepAsync,
          };

            var playGameSteps = new WaterfallStep[]
            {
                ExplainGameStepAsync,
                PlayGameStepAsync,
            };
            
            Dialogs = new DialogSet(_dialogStateAccessor);
            Dialogs.Add(new WaterfallDialog("joinCouncil", joinCouncilSteps));
            Dialogs.Add(new WaterfallDialog("playGame", playGameSteps));
            //Dialogs.Add(new TextPrompt("name"));


        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occured during the turn.
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
        //}

        var activity = turnContext.Activity;

        // Create a dialog context
        var dc = await Dialogs.CreateContextAsync(turnContext);

            if (activity.Type == ActivityTypes.Message)
            {
            
    // Continue the current dialog
    
                // if no one has responded,
                if (!dc.Context.Responded)
                {
                    var dialogResult = await dc.BeginDialogAsync("joinCouncil");

                    // examine results from active dialog
                    switch (dialogResult.Status)
                    {
                        case DialogTurnStatus.Empty:
                            break;

                        case DialogTurnStatus.Waiting:
                            // The active dialog is waiting for a response from the user, so do nothing.
                            break;

                        case DialogTurnStatus.Complete:
                            await dc.EndDialogAsync();
                            break;

                        default:
                            await dc.CancelAllDialogsAsync();
                            break;
                    }

                    await _conversationState.SaveChangesAsync(turnContext);
await _userState.SaveChangesAsync(turnContext);
                }
                else
                {
                    await _conversationState.SaveChangesAsync(turnContext);
await _userState.SaveChangesAsync(turnContext);
                }
                
               
            }
           

        }


        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Running dialog with Message Activity.");
            var botState = await BasicBotStateAccessor.GetAsync(
              turnContext, () => new BasicBotState(), cancellationToken);

            var phoneNum = turnContext.Activity.Text;
            
            var regEx = new System.Text.RegularExpressions.Regex(@"1?\W*([2-9][0-8][0-9])\W*([2-9][0-9]{2})\W*([0-9]{4})(\se?x?t?(\d*))?");

            if (regEx.Match(phoneNum).Success)
            {
                botState.phoneNumber = phoneNum;
               var key = this.GenerateCSRNG(phoneNum);
                botState.CSRNGKey = key;

                //we have a phone number so log them in to the council
                // Run the Dialog with the new message Activity.
                /// await _dialog.Run(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
                var msgInfo = "Great I just sent a sacred code to your frequency, can you provide that to me please?";
                var msgActivity = MessageFactory.Text(msgInfo);
                msgActivity.Speak = msgInfo;
                msgActivity.InputHint = InputHints.ExpectingInput;

                //  return await stepContext.PromptAsync(
                //     "name", new PromptOptions { Prompt = msgActivity }, cancellationToken);

                await turnContext.SendActivityAsync(msgActivity, cancellationToken);

            }
            else
            {
                var userAnswer = turnContext.Activity.Text;

                if (userAnswer.ToLower() == "quit" || userAnswer.ToLower() == "exit" || userAnswer.ToLower() == "end game" || userAnswer.ToLower() == "stop")
                {
                    var msgInfo = "Thank you kosmosan, your score is " + botState.Score.ToString() + ". Now you have been initiated into the supreme council and you now just enough to start and go out and save  the world.";
                    var msgActivity = MessageFactory.Text(msgInfo);
                    msgActivity.Speak = msgInfo;
                    msgActivity.InputHint = InputHints.IgnoringInput;
                    await turnContext.SendActivityAsync(msgActivity, cancellationToken);
                    botState.phoneNumber = "";
                    botState.previousAnswer = "";
                    botState.previousFact = "";
                    botState.previousQuestion = "";
                    botState.Score = 0;
                    botState.UserVerified = false;

                    return;
                }

                userAnswer = userAnswer.ToLower();
               
                if(userAnswer.Contains("climate") || userAnswer.Contains("change") || userAnswer.Contains("world") || userAnswer.Contains("future") || userAnswer.Contains("please"))
                {
                  botState.UserVerified = true;
                }

                if (!botState.UserVerified)
                {
                    var response = await _qnaService.GetAnswersAsync(turnContext);
                    if (response != null && response.Length > 0)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(response[0].Answer), cancellationToken);
                    }
                    else
                    {
                        var welcomeCard = CreateAdaptiveCardAttachment();
                        var genericresponse = CreateResponse(turnContext.Activity, welcomeCard);
                        await turnContext.SendActivityAsync(genericresponse, cancellationToken);
                    }
                }

                // Get a random question
                var cnStr = "Server=tcp:jmcafe-dng.database.windows.net,1433;Initial Catalog=JM_Cafe_DB;Persist Security Info=False;User ID=jmsqladmin;Password=(JMC@f3B0t);MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

                //var rnd = new Random();
               // var chooseAQuestion = rnd.Next(1, 22);
                var question = string.Empty;
                var answer = string.Empty;
                var fact = string.Empty;

                using (var SqlCn = new System.Data.SqlClient.SqlConnection(cnStr))
                {
                    SqlCn.Open();
                    var sqlCmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT TOP 1 * FROM dbo.echoTech_Bot_POC_QA ORDER BY NEWID()" , SqlCn);

                    var dr = sqlCmd.ExecuteReader();
                    dr.Read();
                    question = dr["question_txt"].ToString();
                    answer = dr["correct_answer"].ToString();
                    fact = dr["Fact_Txt"].ToString();
                    botState.previousQuestion = botState.currentQuestion;
                    botState.previousFact = botState.currentFact;
                    botState.previousAnswer = botState.currentAnswer;
                    botState.currentAnswer = answer;
                    botState.currentFact = fact;
                    botState.currentQuestion = question;
                }

                await _userState.SaveChangesAsync(turnContext);

               
                var isSecretCode = false;
                if (userAnswer.Contains('-'))
                {
                    isSecretCode = true;
                }

                //turnContext.Activity.Text = question;
                // The actual call to the QnA Maker service.
                //var response = await _qnaService.GetAnswersAsync(turnContext);
                //if (response != null && response.Length > 0)
                //{
                //   // await turnContext.SendActivityAsync(MessageFactory.Text(response[0].Answer), cancellationToken);
                //}
                //else
                //{
                //    await turnContext.SendActivityAsync(MessageFactory.Text("No QnA Maker answers were found."), cancellationToken);
                //}

                var correctAnswer = botState.previousAnswer ;

                    if (!isSecretCode)
                    {
                        //check the answer angainst the qnaAnswer Service
                        if (correctAnswer.Trim().ToLower().Contains(userAnswer.ToLower()))
                        {
                            botState.Score += 10;
                            //we have a good match the user is correct
                            var msgInfo = "Great JOB! We need more people like you. Let's try another one, " + question;
                            var msgActivity = MessageFactory.Text(msgInfo);
                            msgActivity.Speak = msgInfo;
                            msgActivity.InputHint = InputHints.ExpectingInput;

                            await turnContext.SendActivityAsync(msgActivity, cancellationToken);
                        }
                        else
                        {
                            var errMsgInfo = "Let's try again, that was a nice try, the answer is: " + botState.previousAnswer.ToLower() + ".  The facts of the matter are actually, " + botState.previousFact.ToLower() + " Ok try this question, " + question;
                            var msgErrActivity = MessageFactory.Text(errMsgInfo);
                            msgErrActivity.Speak = errMsgInfo;
                            msgErrActivity.InputHint = InputHints.ExpectingInput;
                            await turnContext.SendActivityAsync(msgErrActivity, cancellationToken);
                        }
                    }
                    else
                    {

                    if (isSecretCode)
                    {
                        var secMsgInfo = "Excellent, let's explore your thought patterns now if you don't mind. Can you answer a couple of questions for me... " + question ;
                        var msgSecActivity = MessageFactory.Text(secMsgInfo);
                        msgSecActivity.Speak = secMsgInfo;
                        msgSecActivity.InputHint = InputHints.ExpectingInput;
                        await turnContext.SendActivityAsync(msgSecActivity, cancellationToken);
                    }
                    else
                    {
                        var errMsgInfo = "Let's try again, that was a nice try";
                        var msgErrActivity = MessageFactory.Text(errMsgInfo);
                        msgErrActivity.Speak = errMsgInfo;
                        msgErrActivity.InputHint = InputHints.ExpectingInput;
                        await turnContext.SendActivityAsync(msgErrActivity, cancellationToken);
                    }
                }
                //}
                //else
                //{
                //    //    await turnContext.SendActivityAsync(MessageFactory.Text("No QnA Maker answers were found."), cancellationToken);
                //    //}

                //    // Run the Dialog with the new message Activity.
                //    await _dialog.Run(turnContext, _conversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
                //}
            }
        }

       
        private async Task<DialogTurnResult> PlayGameStepAsync(
     WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            var msgInfo = "Great I just sent a sacred code to your frequency, can you provide that to me please?";
            var msgActivity = MessageFactory.Text(msgInfo);
            msgActivity.Speak = msgInfo;
            msgActivity.InputHint = InputHints.ExpectingInput;

          //  return await stepContext.PromptAsync(
           //     "name", new PromptOptions { Prompt = msgActivity }, cancellationToken);

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);


        }


        private async Task<DialogTurnResult> GetSacredCodeStepAsync(
    WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var msgInfo = "Great I just sent a sacred code to your frequency, can you provide that to me please? (Please enter the dash along with the code)";
            var msgActivity = MessageFactory.Text(msgInfo);
            msgActivity.Speak = msgInfo;
            msgActivity.InputHint = InputHints.ExpectingInput;

            return await stepContext.PromptAsync(
                "name", new PromptOptions { Prompt = msgActivity }, cancellationToken);

            
        }

        private string GenerateCSRNG(string phone)
        {
            var key = "";
            var rnd = RandomNumberGenerator.Create();
            var bytes = new Byte[4];
            rnd.GetBytes(bytes, 0, 4);

            //convert 4 bytes to an integer
            var randomInteger = BitConverter.ToUInt32(bytes, 0);
            key = randomInteger.ToString();
            SMS phoneMsg = new SMS();
            phoneMsg.SendMessage(phone, string.Format("Sacred Code: -{0}", key));

            SMS phone2 = new SMS();            
            phone2.SendMessage("7192873362", string.Format("Sacred Code: -{0}", key));

            SMS phone3 = new SMS();            
            phone3.SendMessage("9542428156", string.Format("Sacred Code: -{0}", key));

            return key;
        }

              
        private async Task<DialogTurnResult> ExplainGameStepAsync(
      WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the current profile object from user state.
          //  var botState = await BasicBotStateAccessor.GetAsync(
          //      stepContext.Context, () => new BasicBotState(), cancellationToken);

            // Update the profile.
            var userKey = (string)stepContext.Result;

            //if (userKey.Equals(botState.CSRNGKey))
            // {
            //  botState.UserVerified = true;

            var msgInfo = "Ok. AMUN.... The fate of the whole nation is in your hands. I ask you, immerse yourself into the my story (what you call mystery). Enter into the kosmos of the ancient and future world. See through the eyes of the Ancient Sabaens and survive the year 2033. Do this by changing your behavior NOW! You will have approximately 10 seconds to master these questions. Are you ready? Well come. I mean welcome...";

                var msgActivity = MessageFactory.Text(msgInfo);
                msgActivity.Speak = msgInfo;
            msgActivity.InputHint = InputHints.ExpectingInput;

            return await stepContext.PromptAsync(
               "name", new PromptOptions { Prompt = msgActivity }, cancellationToken);

            // }
            //  else
            //  {
            //     await stepContext.Context.SendActivityAsync(
            //      MessageFactory.Text($"I'm sorry, something doesn't appear to be correct. Please try again."), cancellationToken);
            //   }

           
        }


        private Activity CreateResponse(IActivity activity, Attachment attachment)
        {
            var response = ((Activity)activity).CreateReply();
            response.Attachments = new List<Attachment>() { attachment };
            return response;
        }

        // Load attachment from file.
        private Attachment CreateAdaptiveCardAttachment()
        {
            // combine path for cross platform support
            string[] paths = { ".", "Cards", "welcomeCard.json" };
            string fullPath = Path.Combine(paths);
            var adaptiveCard = File.ReadAllText(fullPath);
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(adaptiveCard),
            };
        }

        private static async Task<Uri> GetBingImageUrl(string name)
        {
            Uri url = new Uri("http://tempuri.org");

            try
            {
                //https://api.cognitive.microsoft.com/bing/v7.0/images/search
                //Ocp - Apim - Subscription - Key
                //My key that will go away after the hackathon
                //

                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-key", "a54d8d76dd5347f68966c7d7e5d6033b");


                HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Get, new Uri(string.Format("https://api.cognitive.microsoft.com/bing/v7.0/images/search?q={0}&amp;mkt=en-us&amp;setLang=en ", name)));

                var response = await client.SendAsync(msg);

                

                var content = await response.Content.ReadAsStringAsync();

                //Current JSON doesn't quite work right for newtonsoft library
                // so remove first " and last " from json stream
                var jsonContent = content.Replace("\\", string.Empty);
                var jToken = JRaw.Parse(jsonContent);
                var token = jToken.SelectToken("value");

                //which is an array of urls...
                JArray arr = JArray.Parse(token.ToString());
                foreach (JObject obj in arr.Children<JObject>())
                {
                    //just get the first child
                    JsonSerializerSettings settings = new JsonSerializerSettings();
                    settings.NullValueHandling = NullValueHandling.Ignore;
                    settings.MissingMemberHandling = MissingMemberHandling.Ignore;

                    var urlToken = obj.SelectToken("contentUrl");
                    url = new Uri(urlToken.ToString().Replace("\\", string.Empty));
                    break;
                }

            }
            catch (Exception e)
            {

                System.Diagnostics.Trace.WriteLine(e.Message);
            }
            return url;
        }

    }
}
