using System;
using System.Security.Cryptography;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using Microsoft.Extensions.Logging;

namespace Sphinx.Components
{
    internal class InvalidMetadataComponent : Component
    {
        private readonly ILogger<InvalidMetadataComponent> _logger;

        public InvalidMetadataComponent(ILogger<InvalidMetadataComponent> logger)
        {
            this._logger = logger;
        }

        public override string Id => "InvalidMetadata";
        public override string Name => "Invalid Metadata";

        public override string Description =>
            "Adds invalid metadata to modules to prevent disassembler/decompiler from opening them.";

        public override ComponentUsage Usage => ComponentUsage.Protecting;
        public override int Priority => 0;

        public override void Execute(Context ctx, ExecutionPhase phase)
        {
            if (phase != ExecutionPhase.Apply) return;
            ctx.WriterOptions.WriterEvent += this.WriterOptions_WriterEvent;
        }

        private void WriterOptions_WriterEvent(object sender, ModuleWriterEventArgs e)
        {
            var writer = (ModuleWriterBase) sender;
            switch (e.Event)
            {
                case ModuleWriterEvent.PESectionsCreated:
                {
                    for (var i = 0; i < RandomNumberGenerator.GetInt32(0, 10); i++)
                    {
                        var sect = new PESection("." + Utility.RandomString(), 0x40000040);
                        var size = RandomNumberGenerator.GetInt32(0, byte.MaxValue);
                        var data = new byte[size];
                        RandomNumberGenerator.Fill(data.AsSpan());
                        sect.Add(new ByteArrayChunk(data), 4);
                        e.Writer.AddSection(sect);
                    }

                    break;
                }
                case ModuleWriterEvent.MDEndCreateTables:
                {
                    // TODO: These hurts reflection, add them as an option.
                    /*
                    var methodLen = (uint) writer.Metadata.TablesHeap.MethodTable.Rows + 1;
                    var fieldLen = (uint) writer.Metadata.TablesHeap.FieldTable.Rows + 1;

                    var root = writer.Metadata.TablesHeap.TypeDefTable.Add(new RawTypeDefRow(
                        0, 0x7fff7fff, 0, 0x3FFFD, fieldLen, methodLen));
                    writer.Metadata.TablesHeap.NestedClassTable.Add(new RawNestedClassRow(root, root));

                    var namespaces = writer.Metadata.TablesHeap.TypeDefTable
                        .Select(row => row.Namespace)
                        .Distinct()
                        .ToList();


                    foreach (var ns in namespaces)
                    {
                        if (ns == 0) continue;
                        var type = writer.Metadata.TablesHeap.TypeDefTable.Add(new RawTypeDefRow(
                            0, 0, ns, 0x3FFFD, fieldLen, methodLen));
                        writer.Metadata.TablesHeap.NestedClassTable.Add(new RawNestedClassRow(root, type));
                    }

                    for (uint i = 0; i < (uint) writer.Metadata.TablesHeap.ParamTable.Rows; i++)
                        writer.Metadata.TablesHeap.ParamTable[i].Name = 0x7fff7fff;

                    */

                    writer.Metadata.TablesHeap.ModuleTable.Add(new RawModuleRow(0, 0x7fff7fff, 0, 0, 0));
                    writer.Metadata.TablesHeap.AssemblyTable.Add(new RawAssemblyRow(0, 0, 0, 0, 0, 0, 0, 0x7fff7fff,
                        0));

                    var r = RandomNumberGenerator.GetInt32(8, 16);
                    for (var i = 0; i < r; i++)
                        writer.Metadata.TablesHeap.ENCLogTable.Add(new RawENCLogRow(
                            Convert.ToUInt32(Math.Abs(RandomNumberGenerator.GetInt32(int.MaxValue))),
                            Convert.ToUInt32(Math.Abs(RandomNumberGenerator.GetInt32(int.MaxValue)))));

                    r = RandomNumberGenerator.GetInt32(8, 16);
                    for (var i = 0; i < r; i++)
                        writer.Metadata.TablesHeap.ENCMapTable.Add(new RawENCMapRow(
                            Convert.ToUInt32(Math.Abs(RandomNumberGenerator.GetInt32(int.MaxValue)))));

                    //Utility.Shuffle(writer.MetaData.TablesHeap.NestedClassTable);
                    Utility.Shuffle(writer.Metadata.TablesHeap.ManifestResourceTable);
                    //Utility.Shuffle(writer.MetaData.TablesHeap.GenericParamConstraintTable);

                    // Add extra data. This will break most libraries that open .NET assemblies.
                    // Any value can be written here.
                    writer.TheOptions.MetadataOptions.TablesHeapOptions.ExtraData =
                        Convert.ToUInt32(Math.Abs(RandomNumberGenerator.GetInt32(int.MaxValue)));
                    writer.TheOptions.MetadataOptions.TablesHeapOptions.UseENC = false;
                    writer.TheOptions.MetadataOptions.MetadataHeaderOptions.VersionString += "\0\0\0\0";

                    // The first one is for UnConfuserEx.
                    writer.TheOptions.MetadataOptions.CustomHeaps.Add(
                        new RawHeap("#GUID", Guid.NewGuid().ToByteArray()));
                    writer.TheOptions.MetadataOptions.CustomHeaps.Add(new RawHeap("#Strings", new byte[1]));
                    writer.TheOptions.MetadataOptions.CustomHeaps.Add(new RawHeap("#Blob", new byte[1]));
                    writer.TheOptions.MetadataOptions.CustomHeaps.Add(new RawHeap("#Schema", new byte[1]));

                    // This is normally 16 but setting it to a value less than 14 will fool some
                    // apps into thinking that there's no .NET metadata available
                    writer.TheOptions.PEHeadersOptions.NumberOfRvaAndSizes =
                        Convert.ToUInt32(RandomNumberGenerator.GetInt32(0, 13));


                    break;
                }
                case ModuleWriterEvent.MDOnAllTablesSorted:
                    writer?.Metadata.TablesHeap.DeclSecurityTable.Add(
                        new RawDeclSecurityRow(0x7fff, 0xffff7fff, 0xffff7fff));

                    //writer?.Metadata.TablesHeap.ManifestResourceTable.Add(new RawManifestResourceRow(
                    //    0x7fff7fff, (uint) ManifestResourceAttributes.Private, 0x7fff7fff, 2));
                    break;
            }
        }
    }

    internal class RawHeap : HeapBase
    {
        private readonly byte[] _content;

        public RawHeap(string name, byte[] content)
        {
            this.Name = name;
            this._content = content;
        }

        public override string Name { get; }

        public override uint GetRawLength()
        {
            return (uint) this._content.Length;
        }

        protected override void WriteToImpl(DataWriter writer)
        {
            writer.WriteBytes(this._content);
        }
    }
}