using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.RealtimeConversation;
using RxAI.Realtime.FunctionCalling;
using RxAI.Realtime;
using JSB;
using System.ClientModel;

namespace JSB;
#pragma warning disable OPENAI002

public static class ConversationRunner
{
    public static async Task RunConversationAsync(CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Get the conversation client using RealtimeClientProvider
        RealtimeConversationClientRX conversation =
            RealtimeConversationClientRX.FromAzureCredential(
                new ApiKeyCredential(EnvironmentWellKnown.ApiKey));

        if (conversation == null)
        {
            Console.WriteLine("Failed to get a valid RealtimeConversationClientRX instance.");
            return;
        }

        ConversationSessionOptions options = new()
        {
            //ContentModalities = ConversationContentModalities.Text,
            Instructions = "You are an annoyingly rude assistant. Use lots of sarcasm and emojis.",
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
        conversation.SendAudioAsync(microphone); //no awaiting?
        await conversation.StartResponseTurnAsync();

        // Start sending audio asynchronously and await it
        //var sendAudioTask = conversation.SendAudioAsync(microphone);

        Console.WriteLine("Starting conversation...");

        try
        {
            // Keep the conversation running until cancellation is requested
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            // Handle the cancellation gracefully
            Console.WriteLine("Conversation cancelled.");
        }
        finally
        {
            // Clean up resources
            microphone?.Dispose();
            speakerOutput?.Dispose();
        }
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
}