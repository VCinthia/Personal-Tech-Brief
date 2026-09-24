# Personal Tech Brief — Product Definition

**Status:** Baseline approved  
**Role:** Canonical product definition / source of truth  
**Phase:** Pre-architecture MVP definition

---

## 1. Product vision

Personal Tech Brief is a personal product designed to reduce the effort required to stay current with technologies relevant to the user's present goals.

Its objective is **not** to centralize the largest possible amount of information. Its objective is to identify, prioritize, and present only content with a high probability of being relevant.

The success of the product is therefore not measured by how many sources, articles, releases, posts, or summaries it can expose. It is measured by its ability to **reduce noise and transform scattered information into a small, actionable set of updates**.

Future integrations must preserve this principle. A new source should not automatically result in more content for the user to consume.

### Product principle

> The system may ingest a large amount of information, but it should expose very little.

For example, the system may internally process:

- 200 articles
- 35 releases
- 18 changelogs
- 12 announcements
- 40 posts

while the user's daily result may be only:

- 3 important updates
- 2 optional items for later review
- everything else discarded from the brief or archived internally

This reduction is not a limitation. It is the core value proposition.

---

## 2. User target for the MVP

The MVP is designed for **a single user**.

The target profile is:

> A technology professional who follows multiple topics and information sources but wants to stay current without repeatedly checking feeds, newsletters, websites, repositories, and news streams or being exposed to large volumes of repetitive or low-value content.

The problem is not discovering more information.

The problem is **reducing the cost of identifying what is worth reading**.

Multi-user support, organizations, collaboration, roles, and team workflows are intentionally outside the MVP.

---

## 3. Job to be Done

> When I want to stay current on technologies relevant to my current goals, I want to receive a small and prioritized selection of meaningful updates from sources I consider trustworthy, so I can understand what changed and quickly decide what deserves my attention without manually reviewing every source.

---

## 4. Main product outcome

The primary visible unit is **not an article**.

The primary unit is a **relevant technology update / signal**.

If several sources report the same event, the system should avoid presenting them as separate pieces of content whenever they represent the same underlying update.

Example source inputs:

- Microsoft Blog: `.NET 11 Preview 3 released`
- InfoQ: `Microsoft releases .NET 11 Preview 3`
- Community discussion: `What's interesting about .NET 11 Preview 3?`
- Personal technical blog: `5 things I liked about .NET 11 Preview 3`

The desired product output is conceptually one update:

```text
.NET 11 Preview 3

Priority: High

Why it may matter:
Includes changes related to ASP.NET Core and APIs.

What changed:
...

Why it matters:
...

Related sources:
4
```

### Core modeling principle

> The visible unit is a signal or update, not necessarily a single source item.

---

## 5. MVP input sources

To keep ingestion from becoming a separate project, the MVP supports **RSS/Atom** as the initial permanent source type.

This provides real, heterogeneous information without requiring arbitrary scraping, browser automation, CAPTCHA handling, platform-specific authentication, or fragile integrations.

The conceptual source model may allow different provider types in the future, but the initial implemented adapter should be RSS/Atom.

The MVP may additionally support **manual submission of an article URL** as a one-off input. This does not create a permanent source; it only allows an individual item to enter the analysis flow.

---

## 6. User interests

Interests should be explicit and sufficiently specific.

Broad categories such as `Backend`, `AI`, `Cloud`, or `Programming` are insufficient on their own.

Examples of useful interests include:

- .NET
- ASP.NET Core
- C#
- Python
- FastAPI
- PostgreSQL
- Software Architecture
- Distributed Systems
- Generative AI
- LLM Engineering
- Azure
- Developer Tools

Each interest should have a simple priority:

| Priority | Meaning |
|---|---|
| High | I want to know about important changes in this area |
| Medium | I care when something significant happens |
| Low | It may appear exceptionally, but I do not want routine noise |

The MVP relies on **explicit preferences**, not a complex learned user profile.

Embeddings of the user, automatic preference inference, and personalized machine-learning models are outside the initial scope.

---

## 7. Definition of relevance

An item is not relevant merely because it contains a matching keyword.

Relevance should consider multiple signals, including:

| Signal | Example |
|---|---|
| Match with user interests | ASP.NET Core is a high-priority interest |
| Magnitude of change | Major release vs introductory tutorial |
| Novelty | New information vs already known/repeated content |
| Source authority | Official documentation vs secondary opinion |
| Recency | Recent announcement |
| Redundancy | Multiple sources reporting the same event |
| Potential impact | Breaking change, vulnerability, major capability |
| Previous feedback | Topics consistently considered relevant or irrelevant |

The MVP does not require a perfect relevance algorithm.

It does require the selection criteria to be **explicit, understandable, and testable**.

---

## 8. Conceptual product pipeline

The product should conceptually follow this processing model:

```text
Sources
   ↓
Ingestion
   ↓
Normalization
   ↓
Deduplication
   ↓
Grouping related content
   ↓
Topic classification
   ↓
Relevance evaluation
   ↓
Prioritization
   ↓
Summary / explanation
   ↓
Brief
```

The order is intentional.

In particular:

```text
prioritize
   ↓
summarize
```

is preferred over:

```text
summarize everything
   ↓
decide what to show
```

The system should avoid spending unnecessary processing effort, including LLM usage, on content that is already clearly unsuitable for the final brief.

---

## 9. Role of Generative AI

Generative AI is a capability inside the product, not the product itself.

Potential appropriate uses include:

| Problem | Possible GenAI role |
|---|---|
| Classification | Identify topics represented by content |
| Semantic grouping | Determine whether several articles refer to the same event |
| Relevance support | Provide semantic signals for prioritization |
| Summarization | Explain what happened concisely |
| Contextualization | Explain why an update may matter to the user |

GenAI should **not** become the sole decision-maker for deterministic system behavior.

Rules such as enabled/disabled sources, date boundaries, exact duplicates, result limits, explicit preferences, processing state, and other deterministic constraints should be handled by conventional software logic.

### Design principle

```text
Traditional software
+
GenAI where semantic reasoning adds value
```

not:

```text
Send everything to an LLM
```

---

## 10. The Brief

The brief is the center of the user experience.

The product should not present an infinite feed.

The main dashboard should expose a **finite brief** containing a very small number of updates.

The MVP default is:

> Maximum 5 primary updates per brief.

The system does not need to fill all five positions.

A valid brief may contain:

- 5 items
- 3 items
- 1 item
- 0 items

If there is no sufficiently relevant information, the correct result may be:

> No sufficiently relevant updates were detected since the previous brief.

A small or empty brief is a valid sign that the product is filtering successfully.

---

## 11. Visible information for an update

A brief item should conceptually expose:

| Field | Purpose |
|---|---|
| Title | Identify the update |
| Topic | Explain its domain (.NET, Python, GenAI, etc.) |
| Priority | Show whether it is considered high/medium relevance |
| Summary | Explain what happened |
| Relevance explanation | Explain why it was selected |
| Date | Indicate when the event/update occurred |
| Sources | Identify supporting sources |
| Links | Allow access to original content |
| State | Pending / Read / Saved / Dismissed |

A raw numeric relevance score should not normally be shown to the user.

For example, displaying `Relevance: 0.87342` is considered an internal implementation detail rather than useful product information.

---

## 12. User feedback

The MVP should support a lightweight feedback loop.

Required feedback actions:

- Relevant
- Not relevant
- Save

The system may also register that a user opened an original source.

Initially, this feedback may be used for evaluation and analysis rather than automatic learning.

The MVP does **not** need to promise an adaptive recommendation model.

---

## 13. Ingestion frequency vs consumption frequency

These are separate concerns:

```text
ingestion frequency
≠
consumption frequency
```

The system may ingest and process content several times a day while allowing the user to consume information only when desired.

MVP direction:

- automatic periodic ingestion
- brief available on demand
- no email notifications
- no push notifications
- no proactive alert stream

This supports the central principle that the system works continuously so the user does not have to continuously monitor information.

---

## 14. MVP functional areas

The initial product should contain only four primary functional areas:

| Area | Purpose |
|---|---|
| Brief | Display prioritized updates |
| History | Review previous briefs and previously selected updates |
| Interests | Configure topics and priorities |
| Sources | Configure trusted input sources |

The MVP should avoid complex analytics dashboards and should not include an AI chat interface.

---

## 15. History and non-selected content

An item that does not appear in the top five does not necessarily have to be physically deleted.

The system may distinguish internally between:

```text
ingested content
↓
analyzed content
↓
relevant content
↓
content selected for the brief
```

Only the last category is part of the primary user experience.

History should allow access to previous briefs and previously selected items.

The MVP should **not** introduce a general interface for browsing every ingested item, because doing so recreates the feed behavior the product is intended to avoid.

---

## 16. Search

Search is intentionally outside the MVP.

The initial project should not implement:

- full-text search
- vector search
- semantic search
- RAG over the content corpus
- chat over collected news

These capabilities may be considered later, but including them initially risks transforming the project into a generic RAG application rather than validating the actual product hypothesis.

---

## 17. GitHub Engineering Digest as a post-MVP hypothesis

GitHub Engineering Digest is not a separate feed.

GitHub Releases may later become **another signal provider** entering the same relevance and prioritization pipeline.

Conceptually:

```text
RSS
Blogs
Documentation
GitHub Releases
...
        ↓
shared processing pipeline
        ↓
shared brief
```

The product should **not** evolve into:

```text
News Feed
+
GitHub Feed
+
Release Feed
```

GitHub-derived signals should compete for the same limited brief space as every other source.

An irrelevant release should not appear merely because the integration exists.

A future GitHub integration should therefore be framed as the following hypothesis:

> Incorporating GitHub Releases as an additional signal can improve detection of meaningful changes in technologies or dependencies followed by the user, while preserving the same deduplication and prioritization constraints and without unnecessarily increasing the amount of information presented.

---

## 18. Explicit non-goals for the MVP

The following are intentionally outside the MVP:

- multi-user support
- organizations
- complex roles and permissions
- arbitrary web scraping
- LinkedIn integration
- X/Twitter integration
- Reddit integration
- YouTube integration
- GitHub Releases integration
- newsletter/email ingestion
- outgoing notifications
- mobile application
- collaborative recommendations
- semantic search
- vector database
- RAG
- chatbot
- model training
- personalized machine-learning models
- sophisticated analytics dashboards
- sharing briefs
- comments/social interaction
- automatic social post generation

These are not necessarily undesirable capabilities. They are simply unnecessary for validating the primary hypothesis.

---

## 19. Main product hypothesis

> A combination of selected sources, explicit user preferences, deduplication, semantic grouping, and prioritization can transform a large volume of technology information into a small brief that preserves the updates with the highest value to the user.

This is the primary experiment of the MVP.

---

## 20. Product success signals

The MVP should not optimize for startup-style engagement metrics such as time spent in the product or total content consumed.

Useful evaluation signals include:

| Signal | Interpretation |
|---|---|
| Items ingested vs items shown | Ability to reduce noise |
| Duplicates grouped | Reduction of redundancy |
| Relevant / Not relevant feedback | Perceived recommendation precision |
| Source opens | Generated interest |
| Saves | Perceived long-term value |
| Items per brief | Information load |
| Source distribution | Potential source bias |

One particularly useful evaluation metric is:

```text
recommended items marked relevant
─────────────────────────────────
total recommended items
```

This can be treated informally as **Recommendation Precision**.

No arbitrary target such as 90% is required before a baseline exists.

---

## 21. MVP success criterion

The MVP is considered functionally successful when the following end-to-end workflow exists:

```text
Configure interests
        ↓
Configure trusted sources
        ↓
System obtains content automatically
        ↓
System removes duplicates and groups related updates
        ↓
System evaluates relevance
        ↓
System generates a small brief
        ↓
User quickly understands what happened
        ↓
User decides what is worth reading
        ↓
User can provide feedback
```

This complete workflow, implemented coherently and with appropriate testing, is enough for the MVP.

---

## 22. Technology-selection principle

The project should **not** introduce a technology solely because it is currently fashionable.

The problem and product requirements come first.

Technology choices must later be justified through actual needs, constraints, trade-offs, maintainability, scalability, operational complexity, cost, and learning goals where appropriate.

Examples:

- Python should be introduced where it has a defensible responsibility.
- A message queue should be introduced only if asynchronous decoupling provides real value.
- Azure services should be selected because they solve identified infrastructure needs.
- GenAI should be used only where semantic reasoning materially improves the product.

This principle is intentionally aligned with strong Application Design and Cloud Platform Product and Solution Selection practices: decisions should be explainable, not ornamental.

---

## 23. Product guardrails

The following guardrails should remain true throughout MVP development:

1. The product reduces information load rather than creating another feed.
2. A new source does not automatically justify more visible content.
3. The maximum brief size remains intentionally small.
4. Zero relevant items is a valid output.
5. Prioritization happens before expensive summarization where feasible.
6. GenAI augments deterministic software rather than replacing basic rules.
7. User preferences are explicit in the MVP.
8. Every post-MVP integration must justify how it improves signal quality without increasing unnecessary consumption pressure.
9. Implementation must not silently expand scope beyond documented requirements.
10. Product decisions should be updated in this document before implementation behavior changes.
