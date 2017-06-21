// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*
 *  Some parts of the code have been disabled for testing purposes.
 *  
 */

#define BINARY
namespace Microsoft.ServiceModel {
    using System;
    using System.Collections.Generic;
    using System.Xml;
    using System.IO; // added to use buffered stream
    using System.Runtime;
    using System.ServiceModel.Channels;
    using Microsoft.ServiceModel.Syndication.Resources;


    class XmlBuffer
    {
        List<Section> sections;
        byte[] buffer;
        int offset;
        BufferedStream stream; //BufferedStream - Original: BufferedOutputStream
        BufferState bufferState;
        XmlDictionaryWriter writer;
        XmlDictionaryReaderQuotas quotas;

        enum BufferState
        {
            Created,
            Writing,
            Reading,
        }

        struct Section
        {
            int offset;
            int size;
            XmlDictionaryReaderQuotas quotas;

            public Section(int offset, int size, XmlDictionaryReaderQuotas quotas)
            {
                this.offset = offset;
                this.size = size;
                this.quotas = quotas;
            }

            public int Offset
            {
                get { return this.offset; }
            }

            public int Size
            {
                get { return this.size; }
            }

            public XmlDictionaryReaderQuotas Quotas
            {
                get { return this.quotas; }
            }
        }

        public XmlBuffer(int maxBufferSize)
        {
            if (maxBufferSize < 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("maxBufferSize", maxBufferSize,
                                                                            SR.GetString(SR.ValueMustBeNonNegative)));

            int initialBufferSize = Math.Min(512, maxBufferSize);

            stream = new BufferedStream(new MemoryStream(), initialBufferSize);
           
            sections = new List<Section>(1);
        }

        public int BufferSize
        {
            get
            {
                return buffer.Length;
            }
        }

        public int SectionCount
        {
            get { return this.sections.Count; }
        }

        public XmlDictionaryWriter OpenSection(XmlDictionaryReaderQuotas quotas)
        {
            if (bufferState != BufferState.Created)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            bufferState = BufferState.Writing;
            this.quotas = new XmlDictionaryReaderQuotas();
            quotas.CopyTo(this.quotas);
            
            this.writer = XmlDictionaryWriter.CreateBinaryWriter(stream, null, null, false);
                       
            return this.writer;
        }

        public void CloseSection()
        {
            if (bufferState != BufferState.Writing)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            this.writer.Close(); 
            bufferState = BufferState.Created;

            int size = (int)stream.Length - offset;
            sections.Add(new Section(offset, size, this.quotas));
            offset += size;
        }

        public void Close()
        {
            if (bufferState != BufferState.Created)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            bufferState = BufferState.Reading;
            
            //buffer = stream.ToArray(out bufferSize); NOT SUPPORTED

            //Implementation to do the same that the line above
            buffer = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(buffer, 0, buffer.Length);
           
            writer = null;
            stream = null;
        }

        Exception CreateInvalidStateException()
        {
            return new InvalidOperationException(SR.GetString(SR.XmlBufferInInvalidState));
        }
              

        public XmlDictionaryReader GetReader(int sectionIndex)
        {
            if (bufferState != BufferState.Reading)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            Section section = sections[sectionIndex];
            XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader(buffer, section.Offset, section.Size, null, section.Quotas, null, null);
            
            reader.MoveToContent();
            return reader;
        }
        
        public void WriteTo(int sectionIndex, XmlWriter writer)
        {
            if (bufferState != BufferState.Reading)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateInvalidStateException());
            XmlDictionaryReader reader = GetReader(sectionIndex);
            try
            {
                writer.WriteNode(reader, false);
            }
            finally
            {
                reader.Close();
            }
        }
    }
}