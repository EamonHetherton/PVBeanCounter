using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MackayFisher.Utilities;


namespace Conversations
{
    public class CC_EnviR_Converse : Converse
    {
        public CC_EnviR_Converse(IUtilityLog log)
            : base(log, null)
        {
            // don't bother with ConversationLoader - it is a very simple pattern
            // below creates the conversation definition

            Conversation conv = new Conversation(this, "CC_EnviR_Receive", log);
            Conversations.Add(conv);
            Message message = new Message(conv, MessageType.Find, "");
            Literal literal = new Literal(Element.StringToHex("<msg>", " "), conv);
            message.Elements.Add(literal);
            conv.Messages.Add(message);

            message = new Message(conv, MessageType.ExtractDynamic, "");
            DynamicByteVar variable = new DynamicByteVar("DATA", 10000, conv);
            Variables.Add(variable);
            UseVariable useVar = new UseVariable(conv, "DATA");
            message.Elements.Add(useVar);
            literal = new Literal(Element.StringToHex("</msg>", " "), conv);
            message.Elements.Add(literal);
            conv.Messages.Add(message);
        }
    }
}
