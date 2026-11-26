# Issue-Labeler Workflows

This repository uses actions from [dotnet/issue-labeler](https://github.com/dotnet/issue-labeler) to predict area labels for issues and pull requests.

The following workflow templates were imported and updated from [dotnet/issue-labeler/wiki/Onboarding](https://github.com/dotnet/issue-labeler/wiki/Onboarding):

1. `labeler-cache-retention.yml`
2. `labeler-predict-issues.yml`
3. `labeler-predict-pulls.yml`
4. `labeler-promote.yml`
5. `labeler-train.yml`

## Repository Configuration

Across these workflows, the following changes were made to configure the issue labeler for this repository:

1. Set `LABEL_PREFIX` to `"Area-"`:
    - `labeler-predict-issues.yml`
    - `labeler-predict-pulls.yml`
    - `labeler-train.yml`
2. Remove the `DEFAULT_LABEL` value and input as we do not apply a default label in this repository when no prediction is made.
    - `labeler-predict-issues.yml`
    - `labeler-predict-pulls.yml`
3. Remove the `EXCLUDED_AUTHORS` value as we do not bypass labeling for any authors' issues/pulls in this repository:
    - `labeler-predict-issues.yml`
    - `labeler-predict-pulls.yml`
4. Remove the `repository` input for training the models against another repository:
    - `labeler-train.yml`
5. Update the cache retention cron schedule to an arbitrary time of day:
    - `labeler-cache-retention.yml`

## Pull Request Polling

[`labeler-predict-pulls.yml`](labeler-predict-pulls.yml) polls every five minutes for open pull requests without an `Area-` label, using the last successful scheduled run's start time with five minutes of overlap. The first scheduled run and manual dispatches without pull request numbers scan all open pull requests.

Scheduled and manual runs fail on model cache misses and prediction errors so failed batches do not advance the polling checkpoint. Only `pull_request_target` runs tolerate those failures. The pinned v2.0.0 restore action requires quiet mode to be disabled to enforce cache-miss failures.
