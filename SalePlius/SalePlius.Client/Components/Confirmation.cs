namespace SalePlius.Client.Components;

/// <summary>A question for <see cref="ConfirmModal"/>: what is asked and what runs when the user agrees.</summary>
public record Confirmation(string Title, string Message, Func<Task> Action, string ConfirmText = "Ištrinti");
