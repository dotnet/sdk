## Summary of work required to update TFMs

**Large updates**
- [Branding](https://github.com/dotnet/sdk/blob/main/eng/Versions.props#L6)
  - Recommend doing in the VMR and fixing tests on backflow
  - We had to disable a bunch of tests to merge the branding flow
- [KnownFrameworkReference](https://github.com/dotnet/sdk/blob/main/src/Layout/redist/targets/BundledTemplates.targets) update
  - Should be wrote but tricky to get right
  - Can be done at any time
- Retarget to net11
  - Can be done before runtime work as you typically pin the new TFM to the N-1 versions before you have a runtime
  - Likely requires updating tests
- [Update tests to target the new TFM](https://github.com/dotnet/sdk/blob/main/test/Microsoft.NET.TestFramework/ToolsetInfo.cs#L11)
  - Use PreviousTargetFramework temporarily if you need to pin any tests to the N-1 runtime
  - Disable tests if needed
  - Use a NetTFMUpdate comment to make it easier to track down changes added
 - Update templates
   - Should also be possible to do before runtime update as templating targeting "newTFM" will just be pinned to N-1 

**Miscellaneous**
- Fix versions after the N-1 GA as there should be RC placeholders
- Likely will need to ping to N-1 templates
- Will need to update restore-toolset for the N-1 runtime
- Will need to update global.json for a new SDK


Example PRs from net11 update
[Backflow from VMR](https://github.com/dotnet/sdk/pull/52242)
[unwind test changes](https://github.com/dotnet/sdk/pull/52512)
[Branding](https://github.com/dotnet/sdk/pull/50468)
[BundledVersions](https://github.com/dotnet/sdk/pull/50329)

### Net7 TFM update list

The below is to track the list of changes we had to make to enable 7.0 when we branched in 2021. This will hopefully help us when we do this again in the future.

**Key Maestro PRs**

- SDK to Installer -- https://github.com/dotnet/installer/pull/11750
- Runtime to SDK -- https://github.com/dotnet/sdk/pull/20859
- Runtime to SDK for 7.0 -- https://github.com/dotnet/sdk/pull/19824


**Interesting user commits to SDK**

- Branding -- https://github.com/dotnet/sdk/pull/20212
- Blazor baselines -- https://github.com/dotnet/sdk/pull/20859/commits/564908ede75bfe0e5575ac8246c5e409b9c044ec
- 7.0 feeds -- https://github.com/dotnet/sdk/pull/19824/commits/c26b43e3e20269d04892847c4cbf6641a042b658
- updating template packages -- https://github.com/dotnet/sdk/pull/27486


**Interesting user commits to installer**

- Branding -- https://github.com/dotnet/installer/pull/11566/files
- KnownFrameworkReferences -- https://github.com/dotnet/installer/pull/11750/commits/70c37b7b7b1ad49cf72bbb12dd3c30fef801d7a4
- Update tests for 7.0 -- https://github.com/dotnet/installer/pull/11750/commits/dc2b9d8620ed65c9eb71380ba97b6f0d8f633531
- More test fixes -- https://github.com/dotnet/installer/pull/11750/commits/9a7b55da4d5c777268295326f6be8469f8b4b9f9
- Stage 0 Update -- https://github.com/dotnet/installer/pull/11750/commits/02286315b221895b8181fc350b44bcdf399f7d92
- Install RC version of runtime -- https://github.com/dotnet/installer/commit/ea68501d392ab28b32af5d62946a764e2fadf88c
- Update Templates -- https://github.com/dotnet/installer/pull/12946
- Update DefaultKnownFrameworkReference to be the .0 versions -- https://github.com/dotnet/installer/pull/12654/commits/0e9ba442710970aabef42bbd3a2da69e30f031d4
- Update to the 7.0.0 templates in installer after release -- https://github.com/dotnet/installer/pull/14961
