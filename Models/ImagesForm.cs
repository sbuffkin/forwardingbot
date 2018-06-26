using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.FormFlow.Advanced;
using Microsoft.Bot.Connector;

namespace Microsoft.Bot.Sample.FormBot
{
    [Serializable]
    public class MyAwaitableImage : AwaitableAttachment
    {
        // Mandatory: you should have this ctor as it is used by the recognizer
        public MyAwaitableImage(Attachment source) : base(source) { }

        // Mandatory: you should have this serialization ctor as well & call base
        protected MyAwaitableImage(SerializationInfo info, StreamingContext context) : base(info, context) { }

        // Optional: here you can check for content-type for ex 'image/png' or other..
        public override async Task<ValidateResult> ValidateAsync<T>(IField<T> field, T state)
        {
            var result = await base.ValidateAsync(field, state);

            if (result.IsValid)
            {
                var isValidForMe = this.Attachment.ContentType.ToLowerInvariant().Contains("image/png");

                if (!isValidForMe)
                {
                    result.IsValid = false;
                    result.Feedback = $"Hey, dude! Provide a proper 'image/png' attachment, not any file on your computer like '{this.Attachment.Name}'!";
                }
            }

            return result;
        }

        // Optional: here you can provide additional or override custom help text completely..
        public override string ProvideHelp<T>(IField<T> field)
        {
            var help = base.ProvideHelp(field);

            help += $"{Environment.NewLine}- Only 'image/png' can be attached to this field.";

            return help;
        }

        // Optional: here you can define your custom logic to get the attachment data or add custom logic to check it, etc..
        protected override async Task<Stream> ResolveFromSourceAsync(Attachment source)
        {
            var result = await base.ResolveFromSourceAsync(source);

            // You can apply custom logic to result or avoid calling base and resolve it yourself
            // For ex. if you plan to use your instance several times you can return a MemoryStream instead

            return result;
        }
    }

    [Serializable]
    public class ImagesForm : IDialog<ImagesForm>
    {
        public static Uri _baseUri;


        //[AttachmentContentTypeValidator(ContentType = "pdf")]
        [Prompt("please, provide us your resume")]
        public AwaitableAttachment file_CV;

        [Prompt("Your email ?")]
        public string email;


        public ImagesForm(Uri baseUri)
        {
            _baseUri = baseUri;
        }

        public async Task StartAsync(IDialogContext context)
        {
            var state = this;//new ImagesForm(_baseUri);
            var form = new FormDialog<ImagesForm>(state, BuildForm, FormOptions.PromptInStart);
            context.Call(form, AfterBuildForm);
        }

        private async Task AfterBuildForm(IDialogContext context, IAwaitable<ImagesForm> result)
        {
            context.Done(result);
        }

        public static IForm<ImagesForm> BuildForm()
        {
            OnCompletionAsyncDelegate<ImagesForm> onFormCompleted = async (context, state) =>
            {
                
                var botAccount = new ChannelAccount(name: "mb01-dotnetformflowbot", id: "botframeworktester@outlook.com");
                var userAccount = new ChannelAccount(name: "mb", id: "jason.w.sowers@gmail.com");
                var connector = new ConnectorClient(new Uri("https://email.botframework.com/" ));
                var conversationId = await connector.Conversations.CreateDirectConversationAsync(botAccount, userAccount);
                IMessageActivity message = Activity.CreateMessageActivity();
                message.From = botAccount;
                message.Recipient = userAccount;
                message.Conversation = new ConversationAccount(id: conversationId.Id);
                message.Text = $"Resume recieved from {state.email.ToString()}";
                message.Locale = "en-Us";
                

                var resume = state.file_CV.Attachment;


                //where the file is hosted
                var remoteFileUrl = resume.ContentUrl;
                //where we are saving the file
                var localFileName = HttpContext.Current.Server.MapPath($"~/{resume.Name}"); //@".\" + resume.Name;

                using (var webClient = new WebClient())
                {
                    webClient.DownloadFile(remoteFileUrl, localFileName);
                }

                message.Attachments = new List<Attachment>();
                var absolute = System.Web.VirtualPathUtility.ToAbsolute($"~/{resume.Name}");
                Uri resourceFullPath = new Uri(_baseUri, absolute);

                message.Attachments.Add(new Attachment()
                {
                    ContentUrl = resourceFullPath.AbsoluteUri,
                    ContentType = resume.ContentType,
                    Name = resume.Name
                });
                /*
                var url = resume.ContentUrl;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                using (HttpWebResponse HttpWResp = (HttpWebResponse)req.GetResponse())
                using (Stream responseStream = HttpWResp.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    responseStream.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    Attachment attachment = new Attachment(content: ms, name: resume.Name, contentType: resume.ContentType);
                    message.Attachments.Add(attachment);
                }
                */

                try
                {
                    await connector.Conversations.SendToConversationAsync((Activity)message);
                }
                catch (ErrorResponseException e)
                {
                    Console.WriteLine("Error: ", e.StackTrace);
                }

              
                await context.PostAsync("Here is a summary of the data you submitted:");
 
                var resumeSize = await RetrieveAttachmentSizeAsync(state.file_CV);
                await context.PostAsync($"Your resume is '{state.file_CV.Attachment.Name}' - Type: {state.file_CV.Attachment.ContentType} - Size: {resumeSize} bytes");
                await context.PostAsync($"Your email is {state.email.ToString()}");

                
            };
            return new FormBuilder<ImagesForm>()
                        .Message("Welcome, please submit all required images")
                        .OnCompletion(onFormCompleted)
                        .Build();
        }

        private static async Task<long> RetrieveAttachmentSizeAsync(AwaitableAttachment attachment)
        {
            var stream = await attachment;
            return stream.Length;
        }

        

        /*     
             // Attachment field has no validation - any attachment would work
             public AwaitableAttachment BestImage;

             // Attachment field is optional - validation is done through AttachmentContentTypeValidator usage
             [Optional]
             [AttachmentContentTypeValidator(ContentType = "png")]
             public AwaitableAttachment SecondaryImage;

             // You can use an AwaitableAttachment descendant in order to have your own custom logic
             public IEnumerable<MyAwaitableImage> CustomImages;

             public static IForm<ImagesForm> BuildForm()
             {
                 OnCompletionAsyncDelegate<ImagesForm> onFormCompleted = async (context, state) =>
                 {
                     await context.PostAsync("Here is a summary of the data you submitted:");

                     var bestImageSize = await RetrieveAttachmentSizeAsync(state.BestImage);
                     await context.PostAsync($"Your best image is '{state.BestImage.Attachment.Name}' - Type: {state.BestImage.Attachment.ContentType} - Size: {bestImageSize} bytes");

                     if (state.SecondaryImage != null)
                     {
                         var secondaryImageSize = await RetrieveAttachmentSizeAsync(state.SecondaryImage);
                         await context.PostAsync($"Your secondary image is '{state.SecondaryImage.Attachment.Name}' - Type: {state.SecondaryImage.Attachment.ContentType} - Size: {secondaryImageSize} bytes");
                     }
                     else
                     {
                         await context.PostAsync($"You didn't submit a secondary image");
                     }

                     var customImagesTextInfo = string.Empty;
                     foreach (var image in state.CustomImages)
                     {
                         var imgSize = await RetrieveAttachmentSizeAsync(image);
                         customImagesTextInfo += $"{Environment.NewLine}- Name: '{image.Attachment.Name}' - Type: {image.Attachment.ContentType} - Size: {imgSize} bytes";
                     }

                     await context.PostAsync($"Here is the info of custom images you submitted: {customImagesTextInfo}");
                 };

                 // Form localization is done by setting the thread culture
                 System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-us");

                 return new FormBuilder<ImagesForm>()
                     .Message("Welcome, please submit all required images")
                     .OnCompletion(onFormCompleted)
                     .Build();
             }
     */


    }


}