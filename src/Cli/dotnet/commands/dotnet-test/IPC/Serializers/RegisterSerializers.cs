// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test;

/*
 * NOTE: We have the following ids used for those serializers
 * DO NOT change the IDs of the existing serializers
 * VoidResponseSerializer: 0
 * TestHostProcessExitRequestSerializer: 1
 * TestHostProcessPIDRequestSerializer: 2
 * CommandLineOptionMessagesSerializer: 3
 * ModuleSerializer: 4
 * DiscoveredTestMessageSerializer: 5
 * TestResultMessageSerializer: 6
 * FileArtifactMessageSerializer: 7
 * TestSessionEventSerializer: 8
 * HandshakeMessageSerializer: 9
 */

internal static class RegisterSerializers
{
    public static void RegisterAllSerializers(this NamedPipeBase namedPipeBase)
    {
        namedPipeBase.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        namedPipeBase.RegisterSerializer(new CommandLineOptionMessagesSerializer(), typeof(CommandLineOptionMessages));
        namedPipeBase.RegisterSerializer(new ModuleMessageSerializer(), typeof(ModuleMessage));
        namedPipeBase.RegisterSerializer(new DiscoveredTestMessagesSerializer(), typeof(DiscoveredTestMessages));
        namedPipeBase.RegisterSerializer(new TestResultMessagesSerializer(), typeof(TestResultMessages));
        namedPipeBase.RegisterSerializer(new FileArtifactMessagesSerializer(), typeof(FileArtifactMessages));
        namedPipeBase.RegisterSerializer(new TestSessionEventSerializer(), typeof(TestSessionEvent));
        namedPipeBase.RegisterSerializer(new HandshakeMessageSerializer(), typeof(HandshakeMessage));
    }
}
