using Azure.AI.OpenAI;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AIChatToolService
{
    public class MessageEnhancer
    {
        private readonly ILogger<MessageEnhancer> _logger;
        public MessageEnhancer(ILogger<MessageEnhancer> logger)
        {
            _logger = logger;
        }

        public string getApiKey()
        {
            return System.Environment.GetEnvironmentVariable("OpenAIKey1") ?? throw new Exception("The API Key is missing");
        }



        class EnhancedMessageInput
        {
            public string Message;
            public Language Language;
            public MessageType MessageType;
            public MessageLangueStyle MessageLangueStyle;
        }
        class EnhancedMessageResult
        {
            public EnhancedMessageResult(string EnhancedMessage) {
                this.EnhancedMessage = EnhancedMessage;
            }
            public string EnhancedMessage;
        }

        enum Language
        {
            German,
            English
        }
        enum MessageType
        {
            Chat,
            Email
        }
        enum MessageLangueStyle
        {
            Formal,
            Casual
        }
        [Function("EnhancedMessage")]
        public async Task<HttpResponseData> RunEnhancedMessageAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {

            
            AzureOpenAIClient azureClient = new(
            new Uri("https://aiserviceforchattool.openai.azure.com/"),
            new ApiKeyCredential(getApiKey()));
            ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini");
            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }


            EnhancedMessageInput data = JsonConvert.DeserializeObject<EnhancedMessageInput>(requestBody)!;


            ChatCompletion completion = chatClient.CompleteChat(
                [new UserChatMessage(createPromtEnhancedMessage(data.Message, data.Language, data.MessageType, data.MessageLangueStyle))]);


            Console.WriteLine($"{completion.Role}: {completion.Content[0].Text}");
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            var resultData = JsonConvert.SerializeObject(new EnhancedMessageResult(completion.Content[0].Text));

            await response.WriteStringAsync(resultData);
            return response;
            }
            catch (Exception ex)
            {
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                var resultData = JsonConvert.SerializeObject(ex.Message);
                await response.WriteStringAsync(resultData);
                return response;

            }
        }


        private string createPromtEnhancedMessage(string message, Language language, MessageType type, MessageLangueStyle style)
        {
            var promt = "Bitte verbessere diese Nachricht. Deine Antwort soll ausschließlich die überarbeitete Version der Nachricht enthalten.";

            if (language == Language.English)
            {
                promt += "Bitte antworte auf Englisch.";
            }

            if (language == Language.German)
            {
                promt += "Bitte antworte auf Deutsch.";
            }

            if (type == MessageType.Email)
            {
                promt += "Die Nachricht ist eine E-Mail. Bitte füge auch eine passende Betreffzeile hinzu.";
            }
            if (type == MessageType.Chat)
            {
                promt += "Die Nachricht ist eine Chat-Nachricht.";
            }

            if (style == MessageLangueStyle.Casual)
            {
                promt += "Bitte verwende eine umgangssprachliche Formulierung.";
            }

            if (style == MessageLangueStyle.Formal)
            {
                promt += "Bitte formuliere die Nachricht in einem förmlichen Stil.";
            }


                return promt + $"Nun folgt die Nachricht, die verbessert werden soll: ${message}";
        }
    }
}
