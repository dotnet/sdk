# Add 'Self Update' Command

'dotnetup self update' command - public facing command that updates 'dotnetup' itself
matches 'dotnetup sdk update' nomenclature
'dotnetup update' already updates all of the installs managed by dotnetup. We can consider making it also self update dotnetup later. 

# Self Update Broad Approach

## Windows:


The main dotnetup process, say in dotnetup's official install directory, D/, downloads a new version of 'dotnetup'; the new version of dotnetup is downloaded into a user/temp folder - it is verified either via sha (preview) or signature (stable)

Upon verification, 'dotnetup'  holds a mutex for modifying 'dotnetup' folder state ('dotnetupFolderMutex'). It then deletes any 'D/dotnetup.exe.new' or 'D/dotnetup.exe.old' - if we cannot delete them, fail with the exit code / problem as to why, with 'InsufficientPermissionsToUpdate' (user error) pointing out the other executables are in use. The original new dotnetup executable is copied as '/D/dotnetup.exe.new'


The state of the file system would be:
'D/dotnetup.exe.new'
'D/dotnetup.exe'

'tmp/dotnetup.exe'


'dotnetup.exe' starts 'tmp/dotnetup.exe' and does a heart-beat hand-off via a hidden command, 'dotnetup self replacement <tmp folder executable>': the replacer exe (/tmp/dotnetup.exe) inherits the pipes to stdin/stdout/stderr. 'tmp/dotnetup.exe' acquires a 'FileShare.None' lock on 'D/dotnetup.update.lock' with 'OpenOrCreate', 'ReadWrite', 'FileShare.None'. Once the replacer exe has responded OK (ensure the pid and such match), 'dotnetup.exe' releases 'dotnetupFolderMutex' and exits, upon which 'dotnetupFolderMutex' should be immediately acquired by 'tmp/dotnetup.exe'. If 'dotnetup.exe' fails to get the heart-beat, it fails with a specific error 'DotnetupReplacerCommunicationFailure' type (product error) and tries to kill the 'tmp/dotnetup.exe' process just in case it misbehaved. If 'tmp/dotnetup.exe' cannot acquire the mutex, it should fail mentioning there is another install in place (point to the file).

'tmp/dotnetup.exe' renames 'dotnetup.exe' into 'dotnetup.exe.old' and renames 'dotnetup.exe.new' into 'dotnetup.exe'. It renames 'dotnetup.exe' first to ensure the app had properly exited and nobody restarted it. If they did, then it will bail with a specific error such as 'DotnetupInUseMidUpdateRename' (user error.)

'tmp/dotnetup.exe' will release  'D/dotnetup.update.lock' - it will then run a similar heart beat hand off with the new 'D/dotnetup.exe' with a hidden command, 'dotnetup self replaced <temp folder location to delete, aka this executable>' - if it cannot get a response from 'D/dotnetup.exe' stating 'D/dotnetup.exe' got the lock on 'D/dotnetup.update.lock', it will delete 'D/dotnetup.exe' and rename 'D/dotnetup.exe.old' back to 'D/dotnetup.exe' and report an error; otherwise, it will release 'dotnetupFolderMutex' and close, in which it should signal to 'dotnetup.exe' (the new executable) that it can acquire 'dotnetupFolderMutex'. 

'dotnetup.exe' (now the new executable) will delete the 'tmp/dotnetup.exe' and 'dotnetup.exe.old' and report success with it's version binary, along with an aka.ms link to how to install older versions and close, releasing both locks. 

## Linux:

The running executables can rename themselves...

# Update As a Version Swap Mechanism

Once the releases-index and releases.json files are available, the version to download can be repointed as 'dotnetup self install <channel or version>' and use the same semantics as an 'update'.

# Implementation

DotnetArchiveDownloader -> rename -> DotnetDownloader

DotnetArchiveDownloader in V1 (preview) can use 'ResolveBlobFeedEntry' and use the same unsigned warning and only update off daily channels since that's what exists. We can show progress and download using everything else we already do.

# Release Stable VS Preview

ResolveManifestEntry will resolve an index of dotnetup releases similar to the .NET release manifest.
The manifest will be signed just like the .NET artifacts manifests, with a detached signature, which will be downloaded as well and be used to validate dotnetup's own executable. We could only have an index but supporting multiple versions or allowing a downgrade/revert will only be possible if we maintain separate indexes. Whether we have a 'daily' 'preview' 'stable' keyed index or a 'major.minor' keyed index is not part of this spec.
