// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;

/*
 * NOTE: We have the following ids used for those serializers
 * DO NOT change the IDs of the existing serializers
 * VoidResponseSerializer: 0
 * TestHostCompletedRequestSerializer: 1 (reserved - not registered on the SDK side)
 * TestHostProcessPIDRequestSerializer: 2 (reserved - not registered on the SDK side)
 * CommandLineOptionMessagesSerializer: 3
 * Reserved: 4 (was ModuleSerializer, removed as unused)
 * DiscoveredTestMessagesSerializer: 5
 * TestResultMessagesSerializer: 6
 * FileArtifactMessagesSerializer: 7
 * TestSessionEventSerializer: 8
 * HandshakeMessageSerializer: 9
 * TestInProgressMessagesSerializer: 10
 * AzureDevOpsLogMessageSerializer: 11
 * DisplayMessageSerializer: 12
 */

internal static class RegisterSerializers
{
    public static void RegisterAllSerializers(this NamedPipeBase namedPipeBase)
    {
        namedPipeBase.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        namedPipeBase.RegisterSerializer(new CommandLineOptionMessagesSerializer(), typeof(CommandLineOptionMessages));
        namedPipeBase.RegisterSerializer(new DiscoveredTestMessagesSerializer(), typeof(DiscoveredTestMessages));
        namedPipeBase.RegisterSerializer(new TestResultMessagesSerializer(), typeof(TestResultMessages));
        namedPipeBase.RegisterSerializer(new FileArtifactMessagesSerializer(), typeof(FileArtifactMessages));
        namedPipeBase.RegisterSerializer(new TestSessionEventSerializer(), typeof(TestSessionEvent));
        namedPipeBase.RegisterSerializer(new HandshakeMessageSerializer(), typeof(HandshakeMessage));
        namedPipeBase.RegisterSerializer(new TestInProgressMessagesSerializer(), typeof(TestInProgressMessages));
        namedPipeBase.RegisterSerializer(new AzureDevOpsLogMessageSerializer(), typeof(AzureDevOpsLogMessage));
        namedPipeBase.RegisterSerializer(new DisplayMessageSerializer(), typeof(DisplayMessage));
    }
}
