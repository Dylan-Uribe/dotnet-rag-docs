# Rag.Evals — retrieval evaluation

Measures **retrieval quality**, which is the ceiling on the whole RAG: a chunk that is
never retrieved can never be answered from, no matter how good the chat model is.

This is not a test suite. Tests are binary and run in CI; an eval produces a score you
compare between versions. Nothing here asserts, nothing fails the build.

## What it measures

32 hand-labelled questions over `football.pdf` (the CEFL rulebook). Ground truth is the
set of **pages that actually answer the question**, written by reading the document.

- **recall@k** — share of questions whose answer page appears in the top k results.
- **MRR** — mean reciprocal rank: rewards ranking the answer first, not merely somewhere.

Questions are tagged by kind (`semantic`, `numeric`, `entity`, `lexical`,
`cross-reference`) so a regression can be traced to a category instead of a single number.

## Results

Corpus: `Football.pdf`, 50 pages, 84 chunks (350 tokens, 70 overlap).
Model: `text-embedding-3-small`, 1536 dimensions. TopK = 10.

| Slice | recall@1 | recall@3 | recall@5 | recall@10 | MRR |
|---|---|---|---|---|---|
| **overall (32)** | **0.81** | **0.91** | **0.94** | **0.97** | **0.86** |
| semantic (14) | 0.71 | 0.86 | 0.93 | 1.00 | 0.80 |
| numeric (9) | 0.89 | 0.89 | 0.89 | 0.89 | 0.89 |
| entity (4) | 0.75 | 1.00 | 1.00 | 1.00 | 0.88 |
| lexical (4) | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 |
| cross-reference (1) | 1.00 | 1.00 | 1.00 | 1.00 | 1.00 |

Full per-question output in [results/eval-results.json](results/eval-results.json).

### The one miss

`q05 — "What is the minimum monthly salary in the Open Division?"` (answer on page 9,
Article 16.5). Two causes compound:

1. **Vocabulary mismatch.** The rulebook says *remuneration*, never *salary*.
2. **Chunk dilution.** Article 16.5 is one line inside a 1518-character chunk covering
   all of Article 16, so the chunk embedding is an average dominated by contract law.

Retrieval instead returned page 33 (Open Division prize pool), which matches *money* and
*Open Division* without answering the question. `q09` ("most points from a single
fixture") suffers the same way at rank 10: chunks about *disciplinary* points outrank the
one about league points.

Both are the textbook argument for hybrid search (BM25 + vector), reranking and
small-to-big retrieval. None of those are implemented yet — which is the point of
measuring first.

## Running it

Needs Postgres with pgvector (`docker compose up db`) and `OpenAI:ApiKey` plus
`ConnectionStrings:Postgres` in user secrets — the same secret store as `Rag.API`.

```bash
dotnet run --project Rag.Evals
```

Only the 32 questions are embedded, so a run costs a fraction of a cent.

| Flag | Effect |
|---|---|
| `--reingest` | Drops and re-ingests the corpus. Required after changing chunking. |
| `--out <path>` | Where to write the JSON report. |

---

# Abstention eval

Measures the failure that hurts most in production: **answering a question the corpus
cannot support**. A RAG that invents a plausible rule is worse than one that says nothing.

30 questions: **20 unanswerable** plus **10 answerable controls**. The controls are not
decoration — without them a system that always refuses would score a perfect 1.00 and be
useless. Both failure directions are reported.

Unanswerable questions come in three flavours, hardest last:

| kind | what it probes | example |
|---|---|---|
| `out-of-domain` | Obviously unrelated | "What is the capital of France?" |
| `plausible` | Sounds like it belongs in a rulebook, but is absent | "How much are match officials paid?" |
| `near-miss` | Entities that **do** exist, facts that do not | "What is the seating capacity of the Arena Marítima?" |

The near-miss group is the real test: retrieval returns confident, on-topic chunks, so
only the prompt stops the model from filling the gap.

## Results

| Metric | Value | |
|---|---|---|
| Abstention on unanswerable | **1.00** | 20/20 |
| Answer rate on controls | **1.00** | 10/10 |
| Overall correct | **1.00** | 30/30 |
| Template compliance | **1.00** | 20/20 |

Per kind: `out-of-domain` 5/5, `plausible` 8/8, `near-miss` 7/7. All ten control answers
were also factually correct on inspection. Full output in
[results/abstention-results.json](results/abstention-results.json).

## Is the eval actually measuring anything?

A perfect score proves nothing on its own, so the prompt was deliberately broken — the
line ordering the model to refuse was deleted — and the eval re-run. Abstention collapsed
from 1.00 to 0.00. The eval discriminates.

That experiment also exposed a flaw in the eval itself. With the weakened prompt the
model still refused **18 of 20 times**, just in its own words ("the context does not
specify..."), and the detector counted all of them as hallucinations because it only
matched the template wording. It was measuring *prompt compliance*, not *abstention*.

The detector now distinguishes three responses — template refusal, soft refusal, actual
answer — and reports abstention and template compliance as separate numbers. Re-scoring
the broken-prompt answers with it leaves two flags, and they are instructive:

- **A genuine hallucination.** Asked how many spectators may attend a LAN event, it
  replied with two clubs' venue capacities: real corpus data, wrong question.
- **A false positive the heuristic cannot fix.** Asked the fine for a fifth emergency
  signing, it answered that no such fine exists because Article 20.5 caps signings at
  two. That is a correct, grounded answer — better than refusing — but the ground truth
  is a boolean and cannot express it.

Both limits point the same way: a keyword list can only approximate this judgement. The
principled version needs a judge model, which is the next eval.

---

# Chunking sweep

Re-ingests the corpus under seven chunking configurations and scores the same 32
retrieval questions against each. It only works because the ground truth is page
numbers: chunk ids would be invalidated by every re-chunking.

`TopK` needs no sweep — one retrieval of 10 results already yields recall@1 through
recall@10, so the retrieval eval's own table answers it.

```bash
dotnet run --project Rag.Evals -- --eval sweep
```

Roughly four minutes and well under a cent. The corpus is restored to the configured
chunking when the sweep finishes.

## Results

`ctx tok` is the measured mean token count of the retrieved context per question,
counted with the same `cl100k_base` tokenizer the chunker uses — not an estimate.

| chunk | overlap | chunks | ctx tok | r@1 | r@3 | r@5 | r@10 | MRR | misses |
|---|---|---|---|---|---|---|---|---|---|
| **150** | **30** | **182** | **1260** | 0.78 | **0.97** | **0.97** | **1.00** | **0.87** | **none** |
| 250 | 50 | 115 | 1954 | 0.81 | 0.91 | 0.94 | 0.97 | 0.87 | q05 |
| 350 | 70 | 84 | 2570 | 0.81 | 0.91 | 0.94 | 0.97 | 0.86 | q05 |
| 500 | 100 | 64 | 3376 | 0.69 | 0.84 | 0.88 | 0.91 | 0.78 | q05, q08, q09 |
| 800 | 160 | 50 | 4037 | 0.72 | 0.88 | 0.91 | 0.97 | 0.80 | q05 |
| 350 | 0 | 84 | 2398 | 0.75 | 0.84 | 0.88 | 0.97 | 0.81 | q01 |
| 350 | 175 | 91 | 2917 | 0.75 | 0.91 | 0.94 | 0.97 | 0.84 | q05 |

Row 3 is the current production setting.

## What it says

**150/30 wins on both axes at once.** It answers every question within the top ten and
does it on **half the context tokens** of the 350 baseline. Quality and cost usually
trade against each other; here they do not, which is worth checking twice.

The mechanism is visible per question:

| question | 350/70 | 150/30 |
|---|---|---|
| q05 — minimum Open Division salary | MISS | **2** |
| q09 — most points from one fixture | 10 | **1** |
| q01 — grace period before forfeit | 3 | **1** |

Both known failures were diagnosed as **chunk dilution** — the answer being one line
inside a chunk about something broader. Cutting finer was predicted to fix them before
the sweep ran, and it did. The other half of that prediction was wrong: q11, whose answer
is a six-item list spanning two pages, was expected to break under small chunks and
stayed at rank 1 throughout.

**Overlap barely matters.** At 350 tokens, overlaps of 0, 70 and 175 land within one or
two questions of each other. The only visible effect is q01 dropping to a miss with no
overlap at all.

**500/100 is anomalously bad** — worse than 800/160, which breaks the trend. With 32
questions this cannot be separated from noise.

## Why this is not yet a decision

- **Best-of-seven on one golden set is overfitting.** Picking the winner on the same 32
  questions used to rank it inflates the result. What raises confidence here is the
  mechanism: the gain was predicted from a diagnosis, not discovered by scanning.
- **One question is worth 0.03 of recall**, so any gap under two questions is noise. That
  covers r@1 0.78 vs 0.81, and every difference in the overlap rows.
- **Smaller chunks change what the generator sees.** 182 short fragments instead of 84
  longer ones means less surrounding context per fragment. The abstention eval must be
  re-run at 150/30 before adopting it — retrieval improving says nothing about whether
  the model still refuses correctly.
