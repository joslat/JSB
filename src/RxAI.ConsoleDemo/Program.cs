#pragma warning disable OPENAI002

using System.Text;
using OpenAI.RealtimeConversation;
using RxAI.Realtime.FunctionCalling;
using RxAI.Realtime;
using Spectre.Console;
using JSB;
using System.ClientModel;

Console.OutputEncoding = Encoding.UTF8;

RealtimeConversationClientRX conversation;

bool useOpenAI = false;
if (useOpenAI)
{
    string? openAIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrEmpty(openAIKey))
    {
        Console.WriteLine("Please set the OPENAI_API_KEY environment variable.");
        return;
    }

    conversation =
        RealtimeConversationClientRX.FromOpenAIKey(openAIKey);
}
else
{
    conversation =
        RealtimeConversationClientRX.GetConfiguredClient();
}

if (conversation == null)
{
    Console.WriteLine("Failed to get a valid RealtimeConversationClientRX instance.");
    return;
}

ConversationSessionOptions options = new()
{
    //ContentModalities = ConversationContentModalities.Text,
    Instructions = "You are a funny and smart coach, always ready to help and support the user. Use lots of sarcasm and emojis. Also when the user starts speaking you will pause and listen to him and do not interrupt him.",
    InputTranscriptionOptions = new() { Model = ConversationTranscriptionModel.Whisper1 },
};

// Initialize the conversation
//var calculator = new Calculator();
//await conversation.InitializeSessionAsync(options, FunctionCallingHelper.GetFunctionDefinitions(calculator));

// Use type for static functions, or instance for both static and instance functions
var functionDefinitions = FunctionCallingHelper.GetFunctionDefinitions(typeof(Calculator));
// Initialize the conversation
await conversation.InitializeSessionAsync(options, functionDefinitions);


// Transcription updates
conversation.InputTranscriptionFinishedUpdates.Subscribe(t => AnsiConsole.MarkupLine($"[yellow]{t.Transcript}[/]"));
conversation.OutputTranscriptionFinishedUpdates.Subscribe(u => AnsiConsole.WriteLine());
conversation.OutputTranscriptionDeltaUpdates.Subscribe(u => AnsiConsole.Markup($"[white]{u.Delta}[/]"));

// Function updates
conversation.FunctionCallStarted.Subscribe(f => AnsiConsole.MarkupLine($"[green]Function call: {f.Name}({f.Arguments})[/]"));
conversation.FunctionCallFinished.Subscribe(f => AnsiConsole.MarkupLine($"[green]Function call finished: {f.result}[/]"));

// Cost updates
conversation.SetupCost(5f / 1_000_000, 20f / 1_000_000, 100f / 1_000_000, 200f / 1_000_000);
conversation.TotalCost.Subscribe(c => AnsiConsole.MarkupLine($"[gray]Total cost: {c}[/]"));


// Setup speaker output
SpeakerOutput speakerOutput = new();
conversation.AudioDeltaUpdates.Subscribe(d => speakerOutput.EnqueueForPlayback(d.Delta));

// Setup microphone input
MicrophoneAudioStream microphone = MicrophoneAudioStream.Start();
await conversation.SendAudioAsync(microphone);

// Show potential errors
conversation.ErrorMessages.Subscribe(txt => AnsiConsole.MarkupLine($"[red]Error: {txt}[/]"));

await conversation.StartResponseTurnAsync();

Console.WriteLine("Starting conversation...");

while (true)
{
    await Task.Delay(10);
}

public class Calculator
{
    [FunctionDescription("Get a random number within a specified range")]
    public static int GetRandomNumber(
        [ParameterDescription("min")] int min,
        [ParameterDescription("max")] int max)
    {
        return new Random().Next(min, max);
    }

    [FunctionDescription("Add two numbers together")]
    public static int Add(
        [ParameterDescription("The first number to add")] int a,
        [ParameterDescription("The second number to add")] int b)
    {
        return a + b;
    }
}
