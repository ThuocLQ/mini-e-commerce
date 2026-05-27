param(
    [string]$Topic = "microshop.order-events",
    [int]$Partitions = 3,
    [int]$TimeoutSeconds = 90
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Docker {
    param([string[]]$Arguments)

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker command failed: docker $($Arguments -join ' ')"
    }
}

function Invoke-Kafka {
    param([string[]]$Arguments)

    Invoke-Docker -Arguments (@("exec", "microshop-kafka") + $Arguments)
}

function Wait-KafkaReady {
    param([int]$TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        & docker exec microshop-kafka kafka-topics --bootstrap-server localhost:9092 --list 1>$null 2>$null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 3
    }

    throw "Kafka was not ready after $TimeoutSeconds seconds."
}

function Send-KafkaMessages {
    param(
        [string]$Topic,
        [string[]]$Messages,
        [switch]$ParseKey
    )

    $inputText = $Messages -join "`n"

    if ($ParseKey) {
        $inputText | docker exec -i microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic $Topic --property parse.key=true --property key.separator=:
    }
    else {
        $inputText | docker exec -i microshop-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic $Topic
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to produce Kafka messages."
    }
}

function Read-KafkaMessages {
    param(
        [string]$Topic,
        [string]$Group,
        [int]$TimeoutMs = 10000,
        [switch]$PrintMetadata,
        [switch]$PrintKey
    )

    $args = @(
        "exec", "microshop-kafka",
        "kafka-console-consumer",
        "--bootstrap-server", "localhost:9092",
        "--topic", $Topic,
        "--from-beginning",
        "--timeout-ms", $TimeoutMs
    )

    if ($Group) {
        $args += @("--group", $Group)
    }

    if ($PrintMetadata) {
        $args += @("--property", "print.partition=true", "--property", "print.offset=true")
    }

    if ($PrintKey) {
        $args += @("--property", "print.key=true", "--property", "key.separator= | ")
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & docker @args 2>$null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $text = ($output | Out-String)

    if ($exitCode -ne 0 -and $text -notmatch "Processed a total of") {
        throw "Failed to consume Kafka messages. Output: $text"
    }

    return $text
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$FailureMessage
    )

    if ($Text -notlike "*$Expected*") {
        throw $FailureMessage
    }
}

$runId = (Get-Date).ToString("yyyyMMddHHmmss")
$projectionGroup = "projection-worker-test-$runId"
$analyticsGroup = "analytics-worker-test-$runId"
$sameGroup = "projection-worker-demo-$runId"

Write-Step "Start Kafka and Zookeeper"
Invoke-Docker -Arguments @("compose", "up", "-d", "zookeeper", "kafka")

Write-Step "Wait for Kafka to be ready"
Wait-KafkaReady -TimeoutSeconds $TimeoutSeconds

Write-Step "Create topic $Topic with $Partitions partitions"
Invoke-Kafka -Arguments @("kafka-topics", "--bootstrap-server", "localhost:9092", "--create", "--if-not-exists", "--topic", $Topic, "--partitions", $Partitions, "--replication-factor", "1")

Write-Step "Describe topic"
Invoke-Kafka -Arguments @("kafka-topics", "--bootstrap-server", "localhost:9092", "--describe", "--topic", $Topic)

Write-Step "Produce lesson 25 sample order events"
$messages = @(
    "{`"eventId`":`"11111111-1111-1111-1111-$runId`",`"eventType`":`"OrderCreated`",`"orderId`":`"ORD-$runId-001`",`"customerId`":`"CUST-001`",`"totalAmount`":1977.3,`"currency`":`"VND`",`"occurredAtUtc`":`"2026-05-27T10:00:00Z`",`"testRunId`":`"$runId`"}",
    "{`"eventId`":`"22222222-2222-2222-2222-$runId`",`"eventType`":`"OrderCreated`",`"orderId`":`"ORD-$runId-002`",`"customerId`":`"CUST-002`",`"totalAmount`":299.9,`"currency`":`"VND`",`"occurredAtUtc`":`"2026-05-27T10:01:00Z`",`"testRunId`":`"$runId`"}",
    "{`"eventId`":`"33333333-3333-3333-3333-$runId`",`"eventType`":`"OrderPaid`",`"orderId`":`"ORD-$runId-001`",`"customerId`":`"CUST-001`",`"totalAmount`":1977.3,`"currency`":`"VND`",`"occurredAtUtc`":`"2026-05-27T10:02:00Z`",`"testRunId`":`"$runId`"}"
)
Send-KafkaMessages -Topic $Topic -Messages $messages

Write-Step "Consume from beginning and verify partition/offset output"
$metadataOutput = Read-KafkaMessages -Topic $Topic -TimeoutMs 10000 -PrintMetadata
Write-Host $metadataOutput
Assert-Contains -Text $metadataOutput -Expected $runId -FailureMessage "Consumer did not read the produced test messages."
Assert-Contains -Text $metadataOutput -Expected "Partition:" -FailureMessage "Consumer output did not include partition metadata."
Assert-Contains -Text $metadataOutput -Expected "Offset:" -FailureMessage "Consumer output did not include offset metadata."

Write-Step "Verify two independent consumer groups read the same event stream"
$projectionOutput = Read-KafkaMessages -Topic $Topic -Group $projectionGroup -TimeoutMs 10000
$analyticsOutput = Read-KafkaMessages -Topic $Topic -Group $analyticsGroup -TimeoutMs 10000
Assert-Contains -Text $projectionOutput -Expected $runId -FailureMessage "Projection group did not read produced messages."
Assert-Contains -Text $analyticsOutput -Expected $runId -FailureMessage "Analytics group did not read produced messages."
Write-Host "Projection group: $projectionGroup"
Write-Host "Analytics group:  $analyticsGroup"

Write-Step "Produce keyed messages using OrderId as Kafka key"
$keyedMessages = @(
    "ORD-$runId-100:{`"eventId`":`"77777777-7777-7777-7777-$runId`",`"eventType`":`"OrderCreated`",`"orderId`":`"ORD-$runId-100`",`"customerId`":`"CUST-100`",`"totalAmount`":100,`"currency`":`"VND`",`"occurredAtUtc`":`"2026-05-27T10:10:00Z`",`"testRunId`":`"$runId`"}",
    "ORD-$runId-100:{`"eventId`":`"88888888-8888-8888-8888-$runId`",`"eventType`":`"OrderPaid`",`"orderId`":`"ORD-$runId-100`",`"customerId`":`"CUST-100`",`"totalAmount`":100,`"currency`":`"VND`",`"occurredAtUtc`":`"2026-05-27T10:11:00Z`",`"testRunId`":`"$runId`"}",
    "ORD-$runId-200:{`"eventId`":`"99999999-9999-9999-9999-$runId`",`"eventType`":`"OrderCreated`",`"orderId`":`"ORD-$runId-200`",`"customerId`":`"CUST-200`",`"totalAmount`":200,`"currency`":`"VND`",`"occurredAtUtc`":`"2026-05-27T10:12:00Z`",`"testRunId`":`"$runId`"}"
)
Send-KafkaMessages -Topic $Topic -Messages $keyedMessages -ParseKey

Write-Step "Consume keyed messages and verify key/partition/offset output"
$keyedOutput = Read-KafkaMessages -Topic $Topic -TimeoutMs 10000 -PrintMetadata -PrintKey
Write-Host $keyedOutput
Assert-Contains -Text $keyedOutput -Expected "ORD-$runId-100" -FailureMessage "Consumer output did not include the keyed order id."

Write-Step "Verify consumer group lag command works"
Invoke-Kafka -Arguments @("kafka-consumer-groups", "--bootstrap-server", "localhost:9092", "--describe", "--group", $projectionGroup)

Write-Step "Verify same-group concept with a dedicated group"
$sameGroupOutput = Read-KafkaMessages -Topic $Topic -Group $sameGroup -TimeoutMs 10000
Assert-Contains -Text $sameGroupOutput -Expected $runId -FailureMessage "Same-group smoke consumer did not read produced messages."
Invoke-Kafka -Arguments @("kafka-consumer-groups", "--bootstrap-server", "localhost:9092", "--describe", "--group", $sameGroup)

Write-Host ""
Write-Host "Kafka lesson 25 smoke test passed." -ForegroundColor Green
Write-Host "Topic: $Topic"
Write-Host "TestRunId: $runId"
Write-Host "Checked: topic, partitions, offsets, independent groups, lag command, keyed messages."
