#pragma warning disable OPENAI002

using System.Text;
using System.ClientModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using OpenAI;
using OpenAI.RealtimeConversation;
using RxAI.Realtime.FunctionCalling;
using RxAI.Realtime;
using JSB;
using static JSB.ConversationRunner;

Console.WriteLine("Hello, JSB!");

String operationMode = "noJSB";

if (operationMode == "JSB")
{
    // Run the conversation
    using CancellationTokenSource cts = new CancellationTokenSource();

    // Start the conversation
    Task conversationTask = ConversationRunner.RunConversationAsync(cts.Token);

    // Wait for a key press to cancel
    Console.WriteLine("Press any key to cancel...");

    Console.ReadKey();

    // Cancel the conversation
    cts.Cancel();

    // Await the conversation task to complete
    await conversationTask;
}
else
{
    // Run the conversation
    Console.OutputEncoding = Encoding.UTF8;

    string? openAIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    if (string.IsNullOrEmpty(openAIKey))
    {
        Console.WriteLine("Please set the OPENAI_API_KEY environment variable.");
        return;
    }

    RealtimeConversationClientRX conversation = RealtimeConversationClientRX.FromOpenAIKey(openAIKey);
    //RealtimeConversationClientRX conversation =
    //    RealtimeConversationClientRX.FromAzureCredential(
    //        new ApiKeyCredential(EnvironmentWellKnown.ApiKey));

    ConversationSessionOptions options = new()
    {
        //ContentModalities = ConversationContentModalities.Text,
        Instructions = "You are a nice assistant. You are polite, correct and like to recognize.",
        InputTranscriptionOptions = new() { Model = ConversationTranscriptionModel.Whisper1 },
    };

    // Initialize the conversation
    var calculator = new Calculator();
    await conversation.InitializeSessionAsync(options, FunctionCallingHelper.GetFunctionDefinitions(calculator));

    // Transcription updates
    conversation.InputTranscriptionFinishedUpdates.Subscribe(t => Console.WriteLine(t.Transcript));
    conversation.OutputTranscriptionFinishedUpdates.Subscribe(u => Console.WriteLine());
    conversation.OutputTranscriptionDeltaUpdates.Subscribe(u => Console.Write(u.Delta));

    // Function updates
    conversation.FunctionCallStarted.Subscribe(f => Console.WriteLine($"Function call: {f.Name}({f.Arguments})"));
    conversation.FunctionCallFinished.Subscribe(f => Console.WriteLine($"Function call finished: {f.result}"));

    // Cost updates
    conversation.SetupCost(5f / 1_000_000, 20f / 1_000_000, 100f / 1_000_000, 200f / 1_000_000);
    conversation.TotalCost.Subscribe(c => Console.WriteLine($"Total cost: {c}"));

    // Setup speaker output
    SpeakerOutput speakerOutput = new();
    conversation.AudioDeltaUpdates.Subscribe(d => speakerOutput.EnqueueForPlayback(d.Delta));

    // Setup microphone input
    MicrophoneAudioStream microphone = MicrophoneAudioStream.Start();
    await conversation.SendAudioAsync(microphone);

    await conversation.StartResponseTurnAsync();

    Console.WriteLine("Starting conversation...");

    while (true)
    {
        await Task.Delay(10);
    }
}
