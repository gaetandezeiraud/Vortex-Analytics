## Minimum hardware

The recommended minimum hardware is the following :
* 2 vCPU
* 4 GB RAM
* 40 GB of storage (depending the volume of data you have)

AMD64 and ARM64 are both supported.

## Can I send complexe data?

In short, yes. You are not limited to a simple string for the value of an event.  
But, you need to format it to a string before sending to the API. In general, it's recommanded to convert it to a JSON string because it can be serialize by grafana.

Here an example, we have an event *death_position*, with a value of `{ "x": 100, "y": 200 }` representing the position of the player where he is dead (in general, you also want to know the id of the level). A good way to get the x and y position for each death (and later show the data on a heatmap for example) is to do the following :

1. In the query, get the data `SELECT value FROM "default"."events" WHERE name = 'position'`.
2. Then, go to the "Transform data" tab, click **Add another transformation** and choose **Extract fields**.
3. To finish, select the source (the result of the query) and *JSON* as format. Describe your format structure (here 2 fields with name *x* and *y*) and **Replace all fields**. Now you have a dataset of *x* and *y*, separated that you can use.

## Setup grafana

The first thing you have to do is changing the account password. By default, the username/password combo is admin/admin.   

Then, when it's done, in the left panel of grafana go to **Plugins** and search for the **clickhouse** connector, install it. It's almost done, now go to **Data sources**, click *add new data source*, choose clickhouse and for the **Server address** input, write `clickhouse` and for **Server port** `9000`. Go down **Save & test**. You are good! You can now create a new dashboard in **Dashboards** then **new**.

## Setup clickhouse

We recommend configuring ClickHouse using **clickhouse-disable-logs** or **clickhouse-logs-ttl.xml** to limit log growth. By default, logs increase indefinitely, which can eventually fill the disk if not properly managed.

## Example

### Unique users

```
SELECT uniqExact(identity) AS unique_identities
FROM "default"."events"
WHERE
    tenant_id = 'alpha'
    AND timestamp >= toDateTime64(${__from:date:seconds}, 3)
    AND timestamp <= toDateTime64(${__to:date:seconds}, 3);
```

### Number of start

``` 
SELECT count() AS app_started_count
FROM "default"."events"
WHERE
    tenant_id = 'alpha'
    AND name = 'app_started'
    AND timestamp >= toDateTime64(${__from:date:seconds}, 3)
    AND timestamp <= toDateTime64(${__to:date:seconds}, 3);
```

### Session duration average (in minutes)

```
SELECT
    session_id,
    first_event,
    last_event,
    toFloat64((last_event - first_event) / 60) AS duration_minutes
FROM
(
    SELECT
        session_id,
        MIN(timestamp) AS first_event,
        MAX(timestamp) AS last_event,
        minIf(timestamp, name = 'app_started') AS app_started_ts
    FROM default.events
    PREWHERE tenant_id = 'alpha'
        AND timestamp >= toDateTime64(${__from:date:seconds}, 3)
        AND timestamp <= toDateTime64(${__to:date:seconds}, 3)
    GROUP BY session_id
)
WHERE app_started_ts = first_event
ORDER BY first_event DESC;
``` 

### Session mediane (in minutes)

```
SELECT
    median(duration_minutes)
FROM
(
    SELECT
        session_id,
        toFloat64((MAX(timestamp) - MIN(timestamp)) / 60) AS duration_minutes
    FROM default.events
    PREWHERE tenant_id = 'alpha'
        AND timestamp >= toDateTime64(${__from:date:seconds}, 3)
        AND timestamp <= toDateTime64(${__to:date:seconds}, 3)
    GROUP BY session_id
    HAVING minIf(timestamp, name = 'app_started') = MIN(timestamp)
);
```

### Sessions

```
SELECT
    toDate(timestamp) AS session_start,
    uniqExact(session_id) AS unique_sessions
FROM "default"."events"
WHERE
    tenant_id = 'alpha'
    AND timestamp >= toDateTime(${__from:date:seconds})
    AND timestamp <= toDateTime(${__to:date:seconds})
GROUP BY session_start
ORDER BY session_start;
``` 

### Sessions per hours

```
SELECT
    toStartOfMinute(timestamp) AS session_start,
    uniqExact(session_id) AS unique_sessions
FROM "default"."events"
WHERE
    tenant_id = 'alpha'
    AND timestamp >= toDateTime(${__from:date:seconds})
    AND timestamp <= toDateTime(${__to:date:seconds})
GROUP BY session_start
ORDER BY session_start;
```

### Countries

```
SELECT country, COUNT(country) as country_count FROM "default"."events" WHERE tenant_id = 'alpha' AND name = 'app_started' AND timestamp BETWEEN toDateTime64(${__from:date:seconds}, 3) AND toDateTime64(${__to:date:seconds},3) GROUP BY country ORDER BY country_count DESC
``` 

### Countries Geomap

```
SELECT country, COUNT(country) FROM "default"."events" WHERE tenant_id = 'alpha' AND name = 'app_started' AND timestamp BETWEEN toDateTime64(${__from:date:seconds}, 3) AND toDateTime64(${__to:date:seconds},3) GROUP BY country 
```
