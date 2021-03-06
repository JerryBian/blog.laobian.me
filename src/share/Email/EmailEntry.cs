﻿using System.Collections.Generic;
using System.IO;

namespace Laobian.Share.Email
{
    public class EmailEntry
    {
        public EmailEntry(string toName, string toAddress)
        {
            ToName = toName;
            ToAddress = toAddress;
            Attachments = new Dictionary<string, Stream>();
        }

        public string FromName { get; set; }

        public string FromAddress { get; set; }

        public string ToName { get; set; }

        public string ToAddress { get; set; }

        public string Subject { get; set; }

        public string PlainContent { get; set; }

        public string HtmlContent { get; set; }

        public Dictionary<string, Stream> Attachments { get; }
    }
}