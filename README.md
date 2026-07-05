# ImageSimilarityBot

A Discord bot that detects similar/known images using vector embeddings from a
vision-language model (vLLM serving `TIGER-Lab/VLM2Vec-Full`) and pgvector for
similarity search in PostgreSQL.

When a message contains an image — either as an attachment **or** as a URL in the
message text — the bot embeds it, compares it against a library of source images,
and can delete the message (and optionally ban the author) if it matches a known
image closely enough.

## Architecture

```
┌─────────────┐   messages w/     ┌──────────────────────────┐
│  Discord    │   images or URLs  │  ImageSimilarityBot      │
│  (NetCord)  │ ───────────────▶  │  (.NET 10 worker)        │
└─────────────┘                   └──────────┬───────────────┘
                                             │
                          ┌──────────────────┴──────────────────┐
                          │                                       │
                          ▼                                       ▼
                ┌──────────────────┐                  ┌──────────────────┐
                │  vLLM            │  embeddings      │  PostgreSQL      │
                │  (VLM2Vec-Full)  │ ◀──────────────  │  + pgvector      │
                │  GPU             │                   │  similarity      │
                └──────────────────┘                   └──────────────────┘
```

- **Discord gateway** receives messages, and enqueues any that contain an image
  attachment or an `http`(s) link in the content.
- A background service dequeues each message and processes **both** its image
  attachments and any image URLs found in the text. For each image it:
  hashes it (SHA-256), asks vLLM for an embedding via the OpenAI-compatible
  `/v1/embeddings` endpoint, and stores the result.
- The embedding is stored in PostgreSQL as a `vector(N)` column; similarity is
  computed with pgvector's cosine distance operator.
- On startup the bot runs EF Core migrations and (re)embeds any new/changed
  files in `source_images/`.

## How images are matched and actioned

For every incoming image the bot finds the **nearest source image** by cosine
similarity and computes a similarity score in the range `0.0`–`1.0`
(`1.0` = identical).

- The image is **blocked** when its score is **greater than or equal to** the
  applicable threshold. The threshold is the source image's own
  `SimilarityThreshold` if set, otherwise the global `ActionableThreshold`.
- When an image is blocked, the bot replies in the channel (noting the threshold,
  the similarity score, and the matched source image) and **deletes the message**.
- The author is **banned** if either:
  - the global `Bannable` config flag is `true` (all blocked images ban), or
  - the matched source image is individually marked `Bannable`.

Results are cached in the `AttachmentHistories` table (keyed by hash / original URL
/ proxy URL) so repeat posts are actioned without re-embedding. When source images
change, affected history rows are marked **stale** and re-evaluated on next sighting.

## Discord commands

The bot registers these slash commands:

| Command | Parameters | Description |
|---|---|---|
| `/refresh` | — | Re-scans `source_images/`, re-embeds new/changed files, and reports the total source image count. |
| `/add` | `uri` (string), `bannable` (bool), `threshold` (double, 0.0–1.0) | Downloads the image at `uri` into `source_images/`, writes its metadata, and re-embeds. `threshold` sets how close a match must be to be actioned (higher = stricter). |

The same `/refresh` scan also runs automatically on startup.

## Source image metadata

Each source image may have a sidecar JSON file next to it named
`<image-name>.json` (e.g. `logo.png` → `logo.json`). It holds per-image overrides:

```json
{
  "Bannable": true,
  "SimilarityThreshold": 0.72
}
```

- `SimilarityThreshold` — overrides the global `ActionableThreshold` for this
  image. Omit to fall back to the global value.
- `Bannable` — if `true`, a match against this image bans the author even when
  the global `Bannable` flag is `false`.

The `/add` command writes this file for you when you pass `bannable` / `threshold`.

## Requirements

- Docker with the NVIDIA Container Toolkit (for the vLLM GPU container)
  - On Windows: Docker Desktop with WSL2 backend + NVIDIA driver; enable GPU
    passthrough in Settings → Resources.
  - Verify: `docker run --rm --gpus all nvidia/cuda:12.4.0-base-ubuntu22.04 nvidia-smi`
- An NVIDIA GPU with enough VRAM for VLM2Vec-Full (≈ 12–24 GB depending on
  `VLLM_MAX_MODEL_LEN` and `VLLM_TENSOR_PARALLEL_SIZE`).
- A Discord bot token (with the Message Content + Guild Messages intents, plus
  Guild Moderation if you want the banning feature).

## Quick start

1. Copy the env template and fill in your secrets:

   ```bash
   cp .env.example .env
   ```

   Edit `.env` — at minimum set:
   - `DISCORD_TOKEN` — your Discord bot token
   - `POSTGRES_PASSWORD` — a real password for the DB user
   - `SOURCE_IMAGES_HOST_PATH` — absolute path on the host to your image library
   - `HUGGING_FACE_HUB_TOKEN` — only needed if the model is gated; otherwise
     leave blank (VLM2Vec-Full is public, so you can leave it empty)

2. Build and start everything:

   ```bash
   docker compose up --build
   ```

   The bot waits for both `postgres` and `vllm` to pass their healthchecks
   before starting, so first boot will take a few minutes while vLLM downloads
   and loads the model.

3. Send the bot an image (attachment or link) in a Discord channel it can see.
   It will embed the image and compare against the `source_images` library.

## Configuration (`.env`)

All runtime config is driven by `.env`. The same file controls both vLLM and
the .NET app, so the model the bot requests always matches the model vLLM is
serving.

| Variable | Used by | Description |
|---|---|---|
| `DISCORD_TOKEN` | bot | Discord bot token (read as `Discord:Token` by NetCord) |
| `POSTGRES_USER` | postgres, bot | DB username |
| `POSTGRES_PASSWORD` | postgres, bot | DB password |
| `POSTGRES_DB` | postgres, bot | DB name |
| `VLLM_MODEL` | vllm | HuggingFace model id vLLM downloads & serves |
| `VLLM_SERVED_NAME` | vllm, bot | Name vLLM exposes; the bot sends this in API requests |
| `VLLM_MAX_MODEL_LEN` | vllm | Max context length (default 8192) |
| `VLLM_TENSOR_PARALLEL_SIZE` | vllm | GPUs to shard across (default 1) |
| `HUGGING_FACE_HUB_TOKEN` | vllm | Optional; only for gated models |
| `VLLM_API_KEY` | bot | Bearer token sent to vLLM (use `dummy` if vLLM has no auth) |
| `AI_VECTOR_DIMENSIONS` | bot | Embedding dim — must match the model (VLM2Vec-Full = 3072) |
| `AI_ACTIONABLE_THRESHOLD` | bot | Default similarity score at/above which an image is flagged (per-image metadata can override) |
| `AI_BANNABLE` | bot | Global switch (`AiConfig:Bannable`): if `true`, any blocked image bans the author |
| `SOURCE_IMAGES_HOST_PATH` | bot | Host path bind-mounted to `/app/source_images` |

> **Security:** don't commit real secrets (Discord token, DB password) to source
> control — keep them in `.env` / user secrets, and make sure `appsettings.json`
> doesn't contain live credentials.

### Swapping the model

Change these lines in `.env`:

```env
VLLM_MODEL=<hf-model-id>
VLLM_SERVED_NAME=<name-the-bot-will-request>
AI_VECTOR_DIMENSIONS=<embedding-dim-of-the-model>
```

Then `docker compose up -d --build vllm bot`. The `vector(N)` column type is
sized from `AI_VECTOR_DIMENSIONS`, so if you change dimensions on an existing
database you'll need to drop/recreate the tables (or start with a fresh
`pgdata` volume).

## Services

| Service | Image | Purpose |
|---|---|---|
| `postgres` | `pgvector/pgvector:pg16` | PostgreSQL 16 with the `vector` extension |
| `vllm` | `vllm/vllm-openai:latest` | Serves the embedding model on `:8000` with `--task embedding` |
| `bot` | built from `Dockerfile` | The .NET 10 Discord bot |

## Useful commands

```bash
# Tail logs for all services
docker compose logs -f

# Just the bot
docker compose logs -f bot

# Restart the bot after a code change
docker compose up -d --build bot

# Wipe the database (loses all embeddings)
docker compose down -v

# Check vLLM is serving
curl http://localhost:8000/v1/models
```

## Project layout

```
ImageSimilarityBot/
├── Dockerfile
├── docker-compose.yml
├── .env.example
├── ImageSimilarityBot.slnx
└── ImageSimilarityBot/
    ├── Program.cs                 # Host setup, DI, startup migration/embed, slash commands
    ├── ImageSimilarityContext.cs   # EF Core DbContext with pgvector columns
    ├── appsettings.json
    ├── Interfaces/
    ├── Model/                     # AIConfig, SourceImage, AttachmentHistory, SourceImageMetadata, …
    └── Services/
        ├── IncomingMessageHandler.cs      # Enqueues messages with images/URLs
        ├── QueueHandlerBackgroundService.cs # Dequeues, embeds, matches, actions
        ├── InMemoryMessageQueue.cs
        ├── Sha256Hasher.cs
        ├── SourceImageHandler.cs           # Scans/embeds source_images, handles /add
        └── VllmEmbeddingService.cs
```

## Notes

- The bot runs EF Core migrations on startup, so the database schema is created
  automatically the first time you bring the stack up.
- `source_images/` is re-scanned on every startup and via `/refresh`. New images
  are hashed and embedded; existing ones are skipped only when their hash **and**
  metadata (threshold / bannable) are unchanged. Files removed from the directory
  are deleted from the database.
- Changing a source image (or its metadata) marks existing attachment history as
  stale so previously-seen images are re-evaluated against the updated library.
- The `vllm` healthcheck has a 180s `start_period` because loading a
  multi-GB model takes a while. If your GPU is slow, increase it.