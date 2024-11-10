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

public static class ConversationRunner_V01
{
    public static async Task RunConversationAsync(CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Get the conversation client using RealtimeClientProvider
        RealtimeConversationClientRX conversation = 
            RealtimeConversationClientRX.FromAzureCredential(
                new ApiKeyCredential(Wellknown.AzureOpenAIApiKey));

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

        // Start sending audio asynchronously
        var sendAudioTask = conversation.SendAudioAsync(microphone);

        // Start the response turn
        await conversation.StartResponseTurnAsync();

        Console.WriteLine("Starting conversation...");

        try
        {
            // Wait indefinitely until cancellation is requested
            await Task.Delay(Timeout.Infinite, cancellationToken);
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
            //conversation?.Dispose(); - Not necessary as the client is disposed when the conversation is disposed below -
            //also, it's not implemented in the RealtimeConversationClientRX class

            // Ensure the sendAudioTask is completed
            try
            {
                await sendAudioTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in sendAudioTask: {ex.Message}");
            }
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
