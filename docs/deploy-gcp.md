# Deploying the web app to Google Cloud

The web app runs on [Cloud Run](https://cloud.google.com/run) in the project `silixsoft-mlcalc`,
region `europe-north1` (Finland):

**https://mlcalc-764948534857.europe-north1.run.app**

This document describes the setup, which was done once with the `gcloud` commands below, and how
new versions are deployed.

## How it runs

- **One container, at most one instance.** Each browser tab's workspace lives in the server's
  memory, so every connection of a tab must reach the same instance; with more than one instance
  that would need shared session state. One instance also caps the cost.
- **Scale to zero.** No instance runs while nobody visits, so the service costs next to nothing.
  The first visit after a quiet period waits a few seconds for the instance to start. When the
  instance stops, its workspaces end; the page says so and starts a new one.
- **The configuration is in the repository**: [deploy/cloudrun.yaml](../deploy/cloudrun.yaml)
  holds the instance limits, CPU and memory, probes and the MLCalc limits. Every deploy applies the
  whole file with `gcloud run services replace`, so what runs is what the file says.
- **WebSockets.** SignalR uses one WebSocket per page. Cloud Run ends any request after
  `timeoutSeconds` (one hour, the maximum); the page then reconnects to the same workspace.
- **CPU only during requests.** Cloud Run gives the instance CPU only while a request is open; an
  open page's WebSocket counts. A program that keeps running after its page has closed is slowed
  down until a page connects again.
- **Health.** The startup and liveness probes call `GET /health`. The conventional `/healthz` is
  not used: Cloud Run keeps some paths ending in `z` for itself and answers them with its own 404.
- **Logs** go to Cloud Logging, one line per entry:
  `gcloud run services logs read mlcalc --region europe-north1`

## Who can do what

| Identity | Rights | Used by |
|---|---|---|
| `mlcalc-web@…` | none | the running server, which calls no Google APIs (the default compute account has Editor on the whole project) |
| `github-deployer@…` | `run.developer` on the project, `artifactregistry.writer` on the image repository, may act as `mlcalc-web` | the Deploy workflow |
| `allUsers` | `run.invoker` on the service | visitors: the site is public |

The workflow signs in without a key: GitHub gives the job an OIDC token, which Google's Workload
Identity Federation exchanges for short-lived credentials of `github-deployer`. Google accepts
only tokens from this repository (by its numeric id, which stays the same if the repository is
renamed and can't be taken by another account) and from `main`.

## Deploying a new version

From the **Actions** tab, run **Deploy** on `main` after CI has passed. The workflow:
- builds the image;
- pushes it to Artifact Registry, tagged with the commit;
- applies `deploy/cloudrun.yaml` with that image;
- checks that the new version answers.

The run's page links to the site, and **Deployments** on the repository's main page lists every
deploy.

To deploy by hand instead (from the repository root, signed in with `gcloud auth login`):

```powershell
$tag = git rev-parse HEAD
docker build -t europe-north1-docker.pkg.dev/silixsoft-mlcalc/mlcalc/web:$tag .
docker push europe-north1-docker.pkg.dev/silixsoft-mlcalc/mlcalc/web:$tag
(Get-Content -Raw deploy/cloudrun.yaml).Replace('${PROJECT_ID}', 'silixsoft-mlcalc').Replace('${TAG}', $tag) |
	Set-Content -Encoding ascii $env:TEMP/cloudrun.yaml
gcloud run services replace $env:TEMP/cloudrun.yaml --region europe-north1
```

**Rolling back.** Every deploy creates a new revision; the old ones stay. To list them and to send
all traffic back to an earlier one:

```
gcloud run revisions list --service mlcalc --region europe-north1
gcloud run services update-traffic mlcalc --region europe-north1 --to-revisions <revision>=100
```

Traffic stays pinned to that revision until the next deploy, which sends it to the newest one again.
Artifact Registry keeps the three newest images
([deploy/artifact-cleanup.json](../deploy/artifact-cleanup.json)), so only recent revisions can be
rolled back to.

## Costs

The organisation's billing account has a budget of 10 USD a month, with e-mail alerts at 50, 90 and
100 %. A budget only warns; the limit of one instance is what bounds the cost. Cloud Run's free tier
covers 180 000 vCPU-seconds, 360 000 GiB-seconds and 2 million requests a month, and Artifact
Registry stores 0.5 GB free; three images of about 110 MB each fit. Network traffic out of Google
is charged; a first visit downloads a few megabytes.

## The one-time setup

For reference, or to set the app up again in another project. The commands are for PowerShell,
run by an organisation administrator. The billing account and budget were made in the Cloud
console.

```powershell
$project = 'silixsoft-mlcalc'; $region = 'europe-north1'; $org = '<organisation id>'
$billing = '<billing account id>'; $repoId = '<GitHub repository id>'  # gh api repos/OWNER/REPO --jq .id

# Project, billing, APIs
gcloud projects create $project --name=MLCalc --organization=$org
gcloud billing projects link $project --billing-account=$billing
gcloud config set project $project
gcloud services enable run.googleapis.com artifactregistry.googleapis.com iam.googleapis.com `
	iamcredentials.googleapis.com sts.googleapis.com
$number = gcloud projects describe $project --format='value(projectNumber)'

# Image repository, keeping the three newest images
gcloud artifacts repositories create mlcalc --location=$region --repository-format=docker
gcloud artifacts repositories set-cleanup-policies mlcalc --location=$region `
	--policy=deploy/artifact-cleanup.json --no-dry-run

# Service accounts and their rights
gcloud iam service-accounts create mlcalc-web --display-name='MLCalc web server (runtime, no roles)'
gcloud iam service-accounts create github-deployer --display-name='GitHub Actions deploy workflow'
$deployer = "serviceAccount:github-deployer@$project.iam.gserviceaccount.com"
gcloud projects add-iam-policy-binding $project --member=$deployer --role=roles/run.developer --condition=None
gcloud artifacts repositories add-iam-policy-binding mlcalc --location=$region --member=$deployer `
	--role=roles/artifactregistry.writer
gcloud iam service-accounts add-iam-policy-binding "mlcalc-web@$project.iam.gserviceaccount.com" `
	--member=$deployer --role=roles/iam.serviceAccountUser

# Keyless sign-in from GitHub Actions: this repository's main branch only
gcloud iam workload-identity-pools create github --location=global --display-name='GitHub Actions'
gcloud iam workload-identity-pools providers create-oidc github-actions --location=global `
	--workload-identity-pool=github --issuer-uri='https://token.actions.githubusercontent.com' `
	--attribute-mapping='google.subject=assertion.sub,attribute.repository_id=assertion.repository_id,attribute.ref=assertion.ref' `
	--attribute-condition="assertion.repository_id == '$repoId' && assertion.ref == 'refs/heads/main'"
gcloud iam service-accounts add-iam-policy-binding "github-deployer@$project.iam.gserviceaccount.com" `
	--role=roles/iam.workloadIdentityUser `
	--member="principalSet://iam.googleapis.com/projects/$number/locations/global/workloadIdentityPools/github/attribute.repository_id/$repoId"

# First deploy by hand (see above), then make the service public
gcloud run services add-iam-policy-binding mlcalc --region=$region --member=allUsers --role=roles/run.invoker
```

Making the service public is a one-time step, so the deployer doesn't need the right to change
access (`run.admin`). If the organisation enforces the policy `iam.allowedPolicyMemberDomains`
(domain-restricted sharing, on by default in organisations created since 2024), the `allUsers` grant
is refused until the policy is relaxed for this project.

On GitHub, the repository has the variables `GCP_PROJECT_ID`, `GCP_WIF_PROVIDER`
(`projects/<number>/locations/global/workloadIdentityPools/github/providers/github-actions`) and
`GCP_DEPLOYER_SA` (`github-deployer@<project>.iam.gserviceaccount.com`), and an environment named
`production`. None of these is secret.

**Antivirus that inspects HTTPS.** Some antivirus programs, such as Norton, read HTTPS traffic by
putting a root certificate of their own in front of every site. Windows trusts that certificate,
but `gcloud` has a list of its own, so every call fails with `CERTIFICATE_VERIFY_FAILED`. Give
`gcloud` its usual list plus the antivirus's root certificate with
`gcloud config set core/custom_ca_certs_file <bundle.pem>`.
