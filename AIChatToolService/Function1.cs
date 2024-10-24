using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.AI.OpenAI;
using System.ClientModel;
using OpenAI.Chat;
using Newtonsoft.Json;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection.PortableExecutable;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace AIChatToolService
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;
        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }

        public class AskForUserStroyType
        {
            public string titel;
            public string description;
            public bool alowQuestions;
        }
        public class AskForUserResponse
        {
            public AskForUserResponse(string message, bool final)
            {
                this.message = message;
                this.final = final;
            }
            public string message;
            public bool final { get; set; }
        }

        public async Task<string> getApiKey()
        {
            var keyVaultUri = new Uri("https://aichattoolkeymanager.vault.azure.net/");

            // Use DefaultAzureCredential to fetch the secret (this handles the Managed Identity authentication)
            var client = new SecretClient(vaultUri: keyVaultUri, credential: new DefaultAzureCredential());

            // Get the secret
            KeyVaultSecret secret = await client.GetSecretAsync("key1");

            // Use the secret
            string apiKey = secret.Value;
            return apiKey;
        }

        [Function("AskForUserStroy")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            AzureOpenAIClient azureClient = new(
            new Uri("https://aiserviceforchattool.openai.azure.com/"),
            new ApiKeyCredential("APIKEY"));
            ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini");
            var titel = "Drwaing an Empty Grid";
            var description = "We want to Draw an empty grid for the storage of palets. Use a static json from the backend team";
            string requestBody = string.Empty;

            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            AskForUserStroyType data = JsonConvert.DeserializeObject<AskForUserStroyType>(requestBody)!;


            ChatCompletion completion = chatClient.CompleteChat(
                [new UserChatMessage(createPromtForUserstory(data.titel, data.description, data.alowQuestions))]);

            Console.WriteLine($"{completion.Role}: {completion.Content[0].Text}");

            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                response.Headers.Add("Content-Type", "application/json");
                var final = !completion.Content[0].Text.ToLower().StartsWith("i need more information");
                string responseData = JsonConvert.SerializeObject(new AskForUserResponse(completion.Content[0].Text, final))!;

                await response.WriteStringAsync(responseData);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return response;
        }


        class AnswerType
        {
            public string Titel;
            public string Description;
            public string FirstReponse;
            public string Answer;
        }
        class FinalResult
        {
            public FinalResult(string text)
            {
                this.text = text;
            }
            public string text;
        }



        [Function("AnswerQuestions")]
        public async Task<HttpResponseData> RunAnwerAsync([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            AzureOpenAIClient azureClient = new(
            new Uri("https://aiserviceforchattool.openai.azure.com/"),
            new ApiKeyCredential("APIKEY"));
            ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini");
            string requestBody = string.Empty;
            using (StreamReader streamReader = new(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }


            AnswerType data = JsonConvert.DeserializeObject<AnswerType>(requestBody)!;


            ChatCompletion completion = chatClient.CompleteChat(
                [new UserChatMessage(createPromtForUserstory(data.Titel, data.Description, true)),
                new AssistantChatMessage(data.FirstReponse),
                new UserChatMessage(createPromtAnswerQuerstions(data.Answer))]);


            Console.WriteLine($"{completion.Role}: {completion.Content[0].Text}");
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            var resultData = JsonConvert.SerializeObject(new FinalResult(completion.Content[0].Text));

            await response.WriteStringAsync(resultData);
            return response;
        }


        [Function("test")]
        public async Task<HttpResponseData> test([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            try
            {
                await response.WriteStringAsync(await getApiKey());
                return response;
            }
            catch (Exception ex) {
                await response.WriteStringAsync(ex.InnerException.ToString());
                return response;
            }

        }


        private string createPromtForUserstory(string titel, string description, bool alowQuestions)
        {
            if (alowQuestions)
            {
                return $"Im Creating a user story for azure devops.The titel is: {titel} Here are more Infomation: ${description} Give me the text for \"Description\", \"Development\" and \"Acceptance Criteria\". If you have any Question, then start your response with \"I Need More information\"";
            }

            return $"Im Creating a user story for azure devops.The titel is: {titel} Here are more Infomation: ${description} Give me the text for \"Description\", \"Development\" and \"Acceptance Criteria\". Dont ask any question, just do your job as good as you can";

        }
        private string createPromtAnswerQuerstions(string Answer)
        {
            return $"Here are the answeres to you querions: ${Answer}. Pls Give me the text  for \"Description\", \"Development\" and \"Acceptance Criteria\". Without Asking Any questions";
        }

    }
}
