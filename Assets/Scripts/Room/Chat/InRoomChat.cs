using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Event;
using static FengGameManagerMKII;
using static ChatUtility;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.VersionControl;
using System.Text;

public class InRoomChat : Photon.MonoBehaviour
{
    private const int MaxStoredMessages = 100;
    private const int MaxMessageLength = 1000;
    public static readonly string ChatRPC = "Chat";
    private string inputLine = string.Empty;
    public bool IsVisible = true;
    private readonly List<string> messages = new List<string>();
    public InputField ChatInputField;
    public Text ChatText;

    public void OnGUI()
    {
        if (!IsVisible || (PhotonNetwork.connectionState != ConnectionState.Connected))
        {
            return;
        }
        
        HandleChatInput(this);

        UpdateChat(this);
    }

    /// <summary>
    /// Clears Message List
    /// </summary>
    public void ClearMessages()
    {
        messages.Clear();
    }

    /// <summary>
    /// Adds message to local message list
    /// </summary>
    /// <param name="message"></param>
    public void AddMessage(string message)
    {
        message = message.Trim();
        if (message.Count() <= MaxMessageLength)
        {
            RemoveMessageIfMoreThenMax();
            messages.Add(message);
        }
        else
        {
            OutputErrorMessage($"Message can not have more than {MaxMessageLength} characters");
        }
    }

    /// <summary>
    /// Removes messages when exceding max storage
    /// </summary>
    private void RemoveMessageIfMoreThenMax()
    {
        if (messages.Count() == MaxStoredMessages)
        {
            messages.RemoveAt(0);
        }
    }

    /// <summary>
    /// Send message to all clients on the server
    /// </summary>
    /// <param name="message"></param>
    private void ChatAll(string message)
    {
        if (message.Count() <= 1000)
        {
            if (MarkupIsOk(message))
            {
                var chatMessage = new object[] { message, SetNameColorDependingOnteam(PhotonNetwork.player) };
                instance.photonView.RPC("Chat", PhotonTargets.All, chatMessage);
            }
            else
            {
                OutputErrorMessage("Bad markup.");
            }
        }
        else
        {
            OutputErrorMessage($"Message can not have more than {MaxMessageLength} characters");
        }
    }

    private void HandleChatInput(InRoomChat chat)
    {
        if (current.keyCode == KeyCode.KeypadEnter || current.keyCode == KeyCode.Return)
        {
            if (!string.IsNullOrEmpty(inputLine))
            {
                if (RCEvents.ContainsKey("OnChatInput"))
                {
                    var key = RCVariableNames["OnChatInput"].ToString();
                    if (stringVariables.ContainsKey(key))
                    {
                        stringVariables[key] = chat.inputLine;
                    }
                    else
                    {
                        stringVariables.Add(key, chat.inputLine);
                    }
                }

                if (inputLine.StartsWith("/"))
                {
                    CommandHandler(chat.inputLine);
                }
                else
                {
                    ChatAll(chat.inputLine);
                }

                chat.inputLine = string.Empty;
                chat.ChatInputField.text = string.Empty;
                chat.ChatInputField?.Select();
            }
            else
            {
                chat.ChatInputField?.Select();
            }

            LockControlsToChat(chat);
        }
    }

    /// <summary>
    /// Update GUI chat text
    /// </summary>
    private void UpdateChat(InRoomChat chat)
    {
        StringBuilder messageHandler = new StringBuilder();
        foreach (string message in messages)
        {
            messageHandler.AppendLine(message);
        }

        if (ChatText != null)
        {
            chat.ChatText.text = messageHandler.ToString();
        }

        chat.inputLine = chat.ChatInputField?.text;
    }

    /// <summary>
    /// Handle commands in chat
    /// </summary>
    /// <param name="input"></param>
    private void CommandHandler(string input)
    {
        ChatCommandHandler.CommandHandler(input);
    }

    /// <summary>
    /// Check if message contains valid markup
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private bool MarkupIsOk(string message)
    {
        return Regex.Matches(message, "[<,>]").Count % 2 == 0;
    }

    /// <summary>
    /// If InputField is focused disable hero input, else enable hero input
    /// </summary>
    private void LockControlsToChat(InRoomChat chat)
    {
        foreach (Hero hero in instance.getPlayers())
        {
            if (hero.photonView.isMine)
            {
                if (chat.ChatInputField != null)
                {
                    hero.inputManager.enabled = !chat.ChatInputField.isFocused;
                    break;
                }
            }
        }
    }

    public void OutputSystemMessage(string input)
    {
        var message = $"<color=#FFCC00>{input}</color>"; ;
        instance.chatRoom.AddMessage(message);
    }

    /// <summary>
    /// Formats text as <color=#FF0000>Error: {input}</color>
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public void OutputErrorMessage(string input)
    {
        var message = $"<color=#FF0000>Error: {input}</color>";
        instance.chatRoom.AddMessage(message);
    }

    /// <summary>
    /// Outputs Not Master Client Error to chat
    /// </summary>
    public void OutputErrorNotMasterClient()
    {
        OutputErrorMessage("Not Master Client");
    }

    /// <summary>
    /// Outputs Flayer Not Found Error to chat
    /// </summary>
    /// <param name="playerId"></param>
    public void OutputErrorPlayerNotFound(string playerId)
    {
        OutputErrorMessage($"No player with ID #{playerId} could be found.");
    }
}