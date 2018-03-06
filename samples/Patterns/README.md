# Patterns for working with Cosmos DB

## Overview
* Cosmos DB makes it easy to build scalable applications
* These patterns are exceptions to the rule!
* Quick overview of concepts
* Vanilla (1:1 and 1:N)
* M:N 
* Time-series data
* Write heavy (Event sourcing)
* Handling hot spots

## Basic Concepts
* Documents: 
  * free-form JSON
  * Must have primary key (partition key + id)
* Collections
  * Container for documents
  * Partition key definition
  * Provisioned throughput in RU/s
  * Optional indexing policy (automatic by default)
  * TTL expiration policy
  * Secondary unique key constraints
  * Collections have 1:N partitions hosting partition key ranges based on storage and throughput
  * Collections inherit consistency level, regional configuration from account
* Operations
  * CRUD: GET, POST, PUT, and DELETE
  * SQL queries (single-partition and cross-partition)
  * Intra-partition SPs (transactions)
  * Read feed (scan) and change feed
  * Every operation consumes a fixed RUs
* Thumb rules
  * In order of preference (latency and throughput)
    * GET
    * Single-partition query
    * Cross-partition query 
    * Read feed (or) scan query
  * Bulk Insert (SP) > POST > PUT 
  * TTL Delete > Bulk Delete (SP) > DELETE > PUT
  * Use change feed!

## Vanilla 1:1
* Let's take a gaming use case with 100s to billions of active players
  * GetPlayerById
  * AddPlayer
  * RemovePlayer
  * UpdatePlayer
* Strawman: partition key = `id`
* Only GET, POST, PUT, and DELETE
* Provisioned throughput = Sigma (RUi * Ni)
* Bonus: Bulk Inserts
* Bonus: Bulk Read (use read feed or change feed) for analytics 

## Also Vanilla 1:N
* What if we need to support lookup of game state
  * GetGameByIds(PlayerId, GameId)
  * GetGamesByPlayerId(PlayerId)
  * AddGame
  * RemoveGame
* Partition key is `playerId`
* GetGameByIds, AddGame, and RemoveGame are GET, POST, and DELETE
* GetGamesByPlayerId is a single-partition query: `SELECT * FROM c WHERE c.playerId = ‘p1’`

## What about M:N?
* Multi-player gaming. Lookup by either `gameId` or `playerId`
  * GetPlayerById(PlayerId)
  * GetGameById(GameId)
  * AddGame
  * RemoveGame
* Partition key = PlayerId is OK if mix is skewed towards Player calls (because of index on game ID)
* If mix is 50:50, then need to store two pivots of the same data by Player Id and Game Id
* Double-writes vs. change feed for keeping copies up-to-date

## Time-series data
* Ingest readings from sensors. Perform lookups by date time range
  * AddSensorReading(SensorId)
  * GetReadingsForTimeRange(StartTime, EndTime)
  * GetSensorReadingsForTimeRange(SensorId, StartTime, EndTime)
* No natural partition key. Time is an anti-pattern!
* Set partition key to Sensor ID, id to timestamp, set TTL on the time window
* Bonus: create collections for per-minute, per-hour, per-day windows based on stream aggregation on time-windows
* Bonus: create collections per hour (0-23), and set differential throughput based on the request rates

## Event sourcing pattern
* Write-heavy workloads. Store each event as an immutable document, instead of updating state in-place 
  * AddEventForObject(ObjectId, EventType, Timestamp)
  * GetEventsForObject(ObjectId, EventType)
  * GetEventsSinceTimestamp(Timestamp)
* Why event-driven architectures? 
  * Inserts are more efficient than update at scale
  * Built-in audit log of events
  * Decoupled micro-services that act on events
* GetEventsForObject is a single-partition query to get latest state on read
* GetEventsSinceTimestamp using change feed

## Patterns for hot spots: large documents
* Large documents
  * Consume high RUs due to IOs and indexing over
  * Lead to partition key quota full
  * Lead to rate-limiting 
* Patterns to manage large documents
  * Storing large attributes in separate linked document/collection
  * Storing large attributes in Azure Blob Storage
  * Compress these attributes 
  * Custom indexing policy, disable on subset of properties

## Patterns for hot spots: large partition keys
* Common scenarios:
  * Multi-tenant applications where few tenants are very large
  * Router publishes telemetry at higher rate than sensors
  * Celebrity in a social networking app, viral gaming tournament
* Patterns to manage large partition keys
  * Have a surrogate partition key like tenant ID + 0-100 
  * Use hybrid partitioning scheme for small tenants, and large tenants = 0-100
  * Move large tenants to their own collections
  * If the per-document size is large, use the patterns for large documents

## Patterns for hot spots: frequent partition keys
* Subset of keys much more frequently accessed than others
* Popular item in retail catalog, common driver defect in Windows DnA telemetry
* Patterns to manage hot partition keys
  * Secondary cache collection with just the hot keys
  * Scale out across regions for isolating read and write RUs 
  * Reduce RU consumption by converting critical-path queries to GETs
  * Materialized views for aggregates like COUNT into a document
  * Materialized view for latest state, leaderboard into a document
  * Why? Amortize cost at write time vs. read time


  
  