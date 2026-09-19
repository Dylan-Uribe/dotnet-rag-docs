# Sample questions

These questions work against the sample document [`football.pdf`](../football.pdf)
included in the repo — the *CEFL Official Competition Rulebook, Edition 4.2*, a
fictional esports league rulebook. Ingest it first:

```bash
curl -F "file=@football.pdf" http://localhost:8080/documents
```

Then ask any of the questions below, e.g.:

```bash
curl -X POST http://localhost:8080/query \
  -H "Content-Type: application/json" \
  -d '{ "question": "How many clubs are in the Elite Division?" }'
```

The **expected answer** is what the document actually supports; the model phrases
it in its own words. The **source** column lets you verify it in the PDF. The
`/query` response also returns citations with the page number and cosine distance.

## Questions with expected answers

| # | Question | Expected answer | Source |
| --- | --- | --- | --- |
| 1 | How many clubs are in the Elite Division? | Sixteen. | Art. 4.2 (p. 4) |
| 2 | How many clubs are in the Open Division? | Twenty. | Art. 4.3 (p. 4) |
| 3 | How many regular-season fixtures does an Elite Division club play? | Thirty. | Art. 4.5 (p. 4) |
| 4 | How long is each half of a game? | Six real-time minutes (with a 90-second half-time interval). | Art. 3.3 / 25.2 (p. 2, 12) |
| 5 | How many points does a club get for winning a fixture on aggregate? | Three points, plus one bonus point if it also wins both legs (four maximum). | Art. 26.1 / 26.4 (p. 12) |
| 6 | What is the grace period before a club forfeits for not being present at kick-off? | Eight minutes. | Art. 6.8 (p. 5) |
| 7 | What is the annual fee for an Elite Division licence? | 45,000 credits. | Art. 8.1 (p. 6) |
| 8 | What minimum financial reserve must an Elite Division club maintain? | 120,000 credits. | Art. 9.1 (p. 6) |
| 9 | What is the minimum monthly remuneration in the Elite Division? | 2,400 credits. | Art. 16.4 (p. 9) |
| 10 | When does the Primary Transfer Window open and close? | 1 July to 25 August. | Art. 17.1 (p. 9) |
| 11 | How many overseas players may an Elite Division club register? | Not more than three. | Art. 14.4 (p. 8) |
| 12 | What is the minimum sanction for match manipulation? | A five-year ban from all CEFA activity. | Art. 45.3 (p. 21) |
| 13 | What is the maximum permitted latency for a remote fixture? | 35 milliseconds. | Art. 41.2 (p. 18) |
| 14 | How is a forfeited fixture recorded? | As a 6-0 aggregate defeat (a forfeited leg is 3-0). | Art. 30.1 / 30.2 (p. 13) |
| 15 | Which club has won the most Continental Shields, and how many? | Ríos Atlético, with four. | App. B.4.7 (p. 35) |

## Negative test (out-of-scope question)

This one is **not** answerable from the document — the venue is named, but its
capacity is never stated. It should demonstrate the grounded behaviour: the API
returns *"I could not find an answer to that in the provided documents."* instead
of guessing.

```bash
curl -X POST http://localhost:8080/query \
  -H "Content-Type: application/json" \
  -d '{ "question": "What is the seating capacity of the Arena Marítima?" }'
```
