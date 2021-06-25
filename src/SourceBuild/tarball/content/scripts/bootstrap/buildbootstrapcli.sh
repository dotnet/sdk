#!/usr/bin/env bash
set -e
set -u
set -o pipefail

usage()
{
    echo "Builds a bootstrap CLI from sources"
    echo "Usage: $0 [BuildType] -rid <Rid> -seedcli <SeedCli> [-os <OS>] [-clang <Major.Minor>] [-corelib <CoreLib>]"
    echo ""
    echo "Options:"
    echo "  BuildType               Type of build (-debug, -release), default: -release"
    echo "  -clang <Major.Minor>    Override of the version of clang compiler to use"
    echo "  -corelib <CoreLib>      Path to System.Private.CoreLib.dll, default: use the System.Private.CoreLib.dll from the seed CLI"
    echo "  -os <OS>                Operating system (used for corefx build), default: Linux"
    echo "  -rid <Rid>              Runtime identifier including the architecture part (e.g. rhel.6-x64)"
    echo "  -seedcli <SeedCli>      Seed CLI used to generate the target CLI"
    echo "  -outputpath <path>      Optional output directory to contain the generated cli and cloned repos, default: <Rid>"
}

disable_pax_mprotect()
{
    if [[ $(command -v paxctl) ]]; then
        paxctl -c -m $1
    fi
}

get_max_version()
{
    local maxversionhi=0
    local maxversionmid=0
    local maxversionlo=0
    local maxversiontag
    local versionrest
    local versionhi
    local versionmid
    local versionlo
    local versiontag
    local foundmax

    for d in $1/*; do

        if [[ -d $d ]]; then
            versionrest=$(basename $d)
            versionhi=${versionrest%%.*}
            versionrest=${versionrest#*.}
            versionmid=${versionrest%%.*}
            versionrest=${versionrest#*.}
            versionlo=${versionrest%%-*}
            versiontag=${versionrest#*-}
            if [[ $versiontag == $versionrest ]]; then
                versiontag=""
            fi

            foundmax=0

            if [[ $versionhi -gt $maxversionhi ]]; then
                foundmax=1
            elif [[ $versionhi -eq $maxversionhi ]]; then
                if [[ $versionmid -gt $maxversionmid ]]; then
                    foundmax=1
                elif [[ $versionmid -eq $maxversionmid ]]; then
                    if [[ $versionlo -gt $maxversionlo ]]; then
                    foundmax=1
                    elif [[ $versionlo -eq $maxversionlo ]]; then
                        # tags are used to mark pre-release versions, so a version without a tag
                        # is newer than a version with one.
                        if [[ "$versiontag" == "" || $versiontag > $maxversiontag ]]; then
                            foundmax=1
                        fi
                    fi
                fi
            fi

            if [[ $foundmax != 0 ]]; then
                maxversionhi=$versionhi
                maxversionmid=$versionmid
                maxversionlo=$versionlo
                maxversiontag=$versiontag
            fi
        fi
    done

    echo $maxversionhi.$maxversionmid.$maxversionlo${maxversiontag:+-$maxversiontag}
}

getrealpath()
{
    if command -v realpath > /dev/null; then
        realpath $1
    else
        readlink -e $1
    fi
}

__build_os=Linux
__runtime_id=
__corelib=
__configuration=release
__clangversion=
__outputpath=

while [[ "${1:-}" != "" ]]; do
    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
    -h|--help)
        usage
        exit 1
        ;;
    -rid)
        shift
        __runtime_id=$1
        ;;
    -os)
        shift
        __build_os=$1
        ;;
    -debug)
        __configuration=debug
        ;;
    -release)
        __configuration=release
        ;;
    -corelib)
        shift
        __corelib=`getrealpath $1`
        ;;
    -seedcli)
        shift
        __seedclipath=`getrealpath $1`
        ;;
    -clang)
        shift
        __clangversion=clang$1
        ;;
    -outputpath)
        shift
        __outputpath=$1
        ;;
     *)
    echo "Unknown argument to build.sh $1"; exit 1
    esac
    shift
done


if [[ -z "$__runtime_id" ]]; then
    echo "Missing the required -rid argument"
    exit 2
fi

if [[ -z "$__seedclipath" ]]; then
    echo "Missing the required -seedcli argument"
    exit 3
fi

__build_arch=${__runtime_id#*-}

if [[ -z "$__outputpath" ]]; then
   __outputpath=$__runtime_id/dotnetcli
fi

if [[ -d "$__outputpath" ]]; then
    /bin/rm -r $__outputpath
fi

mkdir -p $__runtime_id
mkdir -p $__outputpath

__outputpath=`getrealpath $__outputpath`

cd $__runtime_id

cp -r $__seedclipath/* $__outputpath

__frameworkversion="2.0.0"
__sdkversion="2.0.0"
__fxrversion="2.0.0"

echo "**** DETECTING VERSIONS IN SEED CLI ****"

__frameworkversion=`get_max_version $__seedclipath/shared/Microsoft.NETCore.App`
__sdkversion=`get_max_version $__seedclipath/sdk`
__fxrversion=`get_max_version $__seedclipath/host/fxr`

echo "Framework version: $__frameworkversion"
echo "SDK version:       $__sdkversion"
echo "FXR version:       $__fxrversion"

__frameworkpath=$__outputpath/shared/Microsoft.NETCore.App/$__frameworkversion

echo "**** DETECTING GIT COMMIT HASHES ****"

# Extract the git commit hashes representig the state of the three repos that
# the seed cli package was built from
__coreclrhash=`strings $__seedclipath/shared/Microsoft.NETCore.App/$__frameworkversion/libcoreclr.so | grep "@(#)" | grep -o "[a-f0-9]\{40\}"`
__corefxhash=`strings $__seedclipath/shared/Microsoft.NETCore.App/$__frameworkversion/System.Native.so | grep "@(#)" | grep -o "[a-f0-9]\{40\}"`
__coresetuphash=`strings $__seedclipath/dotnet | grep "@(#)" | grep -o "[a-f0-9]\{40\}"`
if [[ "$__coresetuphash" == "" ]]; then
   __coresetuphash=`strings $__seedclipath/dotnet | grep -o "[a-f0-9]\{40\}"`
fi

echo "coreclr hash:    $__coreclrhash"
echo "corefx hash:     $__corefxhash"
echo "core-setup hash: $__coresetuphash"

# Clone the three repos if they were not cloned yet. If the folders already
# exist, leave them alone. This allows patching the cloned sources as needed

if [[ ! -d coreclr ]]; then
    echo "**** CLONING CORECLR REPOSITORY ****"
    git clone https://github.com/dotnet/coreclr.git
    cd coreclr
    git checkout $__coreclrhash
    cd ..
fi

if [[ ! -d corefx ]]; then
    echo "**** CLONING COREFX REPOSITORY ****"
    git clone https://github.com/dotnet/corefx.git
    cd  corefx
    git checkout $__corefxhash
    cd ..
fi

if [[ ! -d core-setup ]]; then
    echo "**** CLONING CORE-SETUP REPOSITORY ****"
    git clone https://github.com/dotnet/core-setup.git
    cd  core-setup
    git checkout $__coresetuphash
    cd ..
fi

echo "**** BUILDING CORE-SETUP NATIVE COMPONENTS ****"
cd core-setup
src/corehost/build.sh --configuration $__configuration --arch "$__build_arch" --hostver "2.0.0" --apphostver "2.0.0" --fxrver "2.0.0" --policyver "2.0.0" --commithash `git rev-parse HEAD`
cd ..

echo "**** BUILDING CORECLR NATIVE COMPONENTS ****"
cd coreclr
./build.sh $__configuration $__build_arch $__clangversion -skipgenerateversion -skipmanaged -skipmscorlib -skiprestore -skiprestoreoptdata -skipnuget -nopgooptimize 2>&1 | tee coreclr.log
export __coreclrbin=$(cat coreclr.log | sed -n -e 's/^.*Product binaries are available at //p')
cd ..
echo "CoreCLR binaries will be copied from $__coreclrbin"

echo "**** BUILDING COREFX NATIVE COMPONENTS ****"
corefx/src/Native/build-native.sh $__build_arch $__configuration $__clangversion $__build_os 2>&1 | tee corefx.log
export __corefxbin=$(cat corefx.log | sed -n -e 's/^.*Build files have been written to: //p')
echo "CoreFX binaries will be copied from $__corefxbin"

echo "**** Copying new binaries to dotnetcli/ ****"

# First copy the coreclr repo binaries
cp $__coreclrbin/*so $__frameworkpath
cp $__coreclrbin/corerun $__frameworkpath
cp $__coreclrbin/crossgen $__frameworkpath

# Mark the coreclr executables as allowed to create executable memory mappings
disable_pax_mprotect $__frameworkpath/corerun
disable_pax_mprotect $__frameworkpath/crossgen

# Now copy the core-setup repo binaries

if [[ $__fxrversion == 2* ]]; then
    cp core-setup/cli/exe/dotnet/dotnet $__outputpath
    cp core-setup/cli/exe/dotnet/dotnet $__frameworkpath/corehost

    cp core-setup/cli/dll/libhostpolicy.so $__frameworkpath
    cp core-setup/cli/dll/libhostpolicy.so $__outputpath/sdk/$__sdkversion

    cp core-setup/cli/fxr/libhostfxr.so $__frameworkpath
    cp core-setup/cli/fxr/libhostfxr.so $__outputpath/host/fxr/$__fxrversion
    cp core-setup/cli/fxr/libhostfxr.so $__outputpath/sdk/$__sdkversion
else
    cp core-setup/bin/$__runtime_id.$__configuration/corehost/dotnet $__outputpath
    cp core-setup/bin/$__runtime_id.$__configuration/corehost/dotnet $__frameworkpath/corehost

    cp core-setup/bin/$__runtime_id.$__configuration/corehost/libhostpolicy.so $__frameworkpath
    cp core-setup/bin/$__runtime_id.$__configuration/corehost/libhostpolicy.so $__outputpath/sdk/$__sdkversion

    cp core-setup/bin/$__runtime_id.$__configuration/corehost/libhostfxr.so $__frameworkpath
    cp core-setup/bin/$__runtime_id.$__configuration/corehost/libhostfxr.so $__outputpath/host/fxr/$__fxrversion
    cp core-setup/bin/$__runtime_id.$__configuration/corehost/libhostfxr.so $__outputpath/sdk/$__sdkversion
fi

# Mark the core-setup executables as allowed to create executable memory mappings
disable_pax_mprotect $__outputpath/dotnet
disable_pax_mprotect $__frameworkpath/corehost

# Finally copy the corefx repo binaries
cp $__corefxbin/**/System.* $__frameworkpath

# Copy System.Private.CoreLib.dll override from somewhere if requested
if [[ "$__corelib" != "" ]]; then
    cp "$__corelib" $__frameworkpath
fi

# Add the new RID to Microsoft.NETCore.App.deps.json
# Replace the linux-x64 RID in the target, runtimeTarget and runtimes by the new RID
# and add the new RID to the list of runtimes.
echo "**** Adding new rid to Microsoft.NETCore.App.deps.json ****"

#TODO: add parameter with the parent RID sequence

sed \
    -e 's/runtime\.linux-x64/runtime.'$__runtime_id'/g' \
    -e 's/runtimes\/linux-x64/runtimes\/'$__runtime_id'/g' \
    -e 's/Version=v\([0-9].[0-9]\)\/linux-x64/Version=v\1\/'$__runtime_id'/g' \
$__seedclipath/shared/Microsoft.NETCore.App/$__frameworkversion/Microsoft.NETCore.App.deps.json \
>$__frameworkpath/Microsoft.NETCore.App.deps.json

# add the new RID to the list of runtimes iff it does not already exist (sed inplace)
__os_dependencies=
if [[ $__build_os == "Linux" ]]; then
    __os_dependencies='"linux", "linux-'$__build_arch'", '
fi
grep -q "\"$__runtime_id\":" $__frameworkpath/Microsoft.NETCore.App.deps.json || \
sed -i \
    -e 's/"runtimes": {/&\n    "'$__runtime_id'": [\n      '"$__os_dependencies"'"unix", "unix-'$__build_arch'", "any", "base"\n    ],/g' \
$__frameworkpath/Microsoft.NETCore.App.deps.json

__crossgentimeout=120

function crossgenone(){
    echo $2/crossgen /MissingDependenciesOK /Platform_Assemblies_Paths $2:$3 /in $1 /out $1.ni >$1.log 2>&1
    timeout $__crossgentimeout $2/crossgen /MissingDependenciesOK /Platform_Assemblies_Paths $2:$3 /in $1 /out $1.ni >>$1.log 2>&1
    exitCode=$?
    if [ "$exitCode" == "0" ]
    then
        rm $1.log
        mv $1.ni $1
    elif grep -q -e 'The module was expected to contain an assembly manifest' \
                 -e 'An attempt was made to load a program with an incorrect format.' \
                 -e 'File is PE32' $1.log
    then
        rm $1.log
        echo "$1" >> crossgenskipped
    else
        echo "$1" >> crossgenretry
    fi
}

# Run an assembly through ildasm ilasm roundtrip to remove x64 crossgen
function uncrossgenone(){
    echo >> $1.log 2>&1
    echo mv $1 $1.x64 >> $1.log 2>&1
    echo $2/ildasm -raweh -out=$1.il $1.x64 "&& \\" >> $1.log 2>&1
    echo $2/ilasm -output=$1 -QUIET -NOLOGO -DEBUG -OPTIMIZE $1.il >> $1.log 2>&1

    mv $1 $1.x64
    $2/ildasm -raweh -out=$1.il $1.x64 && \
    $2/ilasm -output=$1 -DLL -QUIET -NOLOGO -DEBUG -OPTIMIZE $1.il
    exitCode=$?
    if [ "$exitCode" == "0" ]
    then
        rm $1.x64
        rm $1.il
    else
        echo "$1" >> uncrossgenfails
    fi
}

# if $__build_arch is not x64 then any dll which was crossgened for x64 must be recrossgened for $__build_arch
if [[ "$__build_arch" != "x64" ]]
then
    echo "**** Beginning crossgen for $__build_arch target  ****"
    export -f crossgenone
    export __crossgentimeout

    rm -f crossgenretry crossgendlls crossgenskipped uncrossgenfails

    # Assumes System.Private.CoreLib was already crossgened
    find $__outputpath -type f -name \*.dll -or -name \*.exe | grep -v System.Private.CoreLib > crossgendlls

    cat crossgendlls | xargs -P 0 -n 1 -I {} bash -c 'crossgenone "$@"' _ {} "$__frameworkpath" "$__outputpath/sdk/$__sdkversion"

    echo
    echo "**** Crossgen skipped for non-managed assembly files:"
    echo

    touch crossgenskipped
    cat crossgenskipped

    echo
    echo "**** Crossgen failed for the following dlls:"
    echo

    touch crossgenretry
    cat crossgenretry

    echo
    echo "**** Beginning uncrossgen for failed dlls  ****"
    echo
    export -f uncrossgenone

    rm -f $__coreclrbin/System.Private.CoreLib.dll
    ln -s $__corelib $__coreclrbin/System.Private.CoreLib.dll

    cat crossgenretry | xargs -P 0 -n 1 -I {} bash -c 'uncrossgenone "$@"' _ {} "$__coreclrbin"

    rm -f $__coreclrbin/System.Private.CoreLib.dll

    echo
    echo "**** Uncrossgen failed for the following dlls:"
    echo
    touch uncrossgenfails
    cat uncrossgenfails
fi

echo "**** Bootstrap CLI was successfully built  ****"

