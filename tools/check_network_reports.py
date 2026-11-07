"""Pair NET-02 schema-2 reports without opening the game or treating them as full acceptance.

Reports are observations from two local processes, not authenticated transport transcripts.
Only their shared session/fixture/player/request identities are compared across endpoints;
clocks, generations, thread IDs and connection tokens belong to each endpoint separately.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import sys
from uuid import UUID

ROOT = Path(__file__).resolve().parents[1]
MAX_REPORT_BYTES = 2 * 1024 * 1024
REQUEST = "XYS/Request/1"
REJECTED = "XYS/Rejected/1"
HELLO = "XYS/Hello/1"
WELCOME = "XYS/Welcome/1"
TRACE_KINDS = ("Reset", "Processed", "StaleQueue", "SendAttempt", "SendSuppressed", "ProbeAttempt", "Faulted")
RECEIVE_KINDS = ("Ignored", "RateLimited", "InvalidMessage", "HelloAccepted", "WelcomeAccepted", "RejectionProduced", "RejectionAccepted")
REASONS = ("WritesDisabled", "HandshakeRequired", "WrongSession", "ActorMismatch", "InvalidRequest")
PROBES = ("Start", "Collect", "ActorMismatch", "WrongSession", "Malformed")
EXPECTED = ("WritesDisabled", "WritesDisabled", "ActorMismatch", "WrongSession", "InvalidRequest")


def require(value, message):
    if not value:
        raise ValueError(message)


def integer(value, label, minimum=0, maximum=2**63 - 1):
    require(type(value) is int and minimum <= value <= maximum, f"Invalid integer: {label}")
    return value


def identity(value, label):
    integer(value, label, -(2**63))
    require(value != 0, f"Zero player identity: {label}")
    return value


def guid(value, label):
    require(isinstance(value, str) and re.fullmatch(r"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", value),
            f"Invalid GUID: {label}")
    parsed = UUID(value)
    require(parsed.int != 0, f"Empty GUID: {label}")
    return str(parsed)


def digest(value, label):
    require(isinstance(value, str) and re.fullmatch(r"[0-9a-fA-F]{64}", value), f"Invalid SHA-256: {label}")
    return value.lower()


def enum(value, names, label):
    if type(value) is int and 0 <= value < len(names):
        return names[value]
    require(isinstance(value, str) and value in names, f"Unknown enum: {label}")
    return value


def obj(value, label):
    require(isinstance(value, dict), f"Missing object: {label}")
    return value


def array(value, label):
    require(isinstance(value, list), f"Missing array: {label}")
    return value


def peer(value):
    value = obj(value, "peer")
    require(type(value.get("IsHost")) is bool and value.get("Ready") is True, "Peer is not a ready role")
    return {"PlayerId": identity(value.get("PlayerId"), "peer ID"), "IsHost": value["IsHost"], "Ready": True,
            "Challenge": guid(value.get("Challenge"), "peer challenge"),
            "ConnectionId": guid(value.get("ConnectionId"), "local peer connection")}


def trace(value):
    value = obj(value, "trace")
    result = dict(value)
    for key in ("Sequence", "Milliseconds", "WorldGeneration", "DisconnectGeneration", "ThreadId"):
        result[key] = integer(value.get(key), "trace " + key, 1 if key in ("Sequence", "ThreadId") else 0)
    result["Kind"] = enum(value.get("Kind"), TRACE_KINDS, "trace kind")
    observation = value.get("Observation")
    if observation is not None:
        observation = dict(obj(observation, "observation"))
        observation["Kind"] = enum(observation.get("Kind"), RECEIVE_KINDS, "receive kind")
        require(type(observation.get("BodyReadAttempted")) is bool, "Missing body read observation")
        for key in ("RequestId", "Challenge"):
            observation[key] = guid(observation[key], key) if observation.get(key) is not None else None
        observation["Reason"] = enum(observation["Reason"], REASONS, "reason") if observation.get("Reason") is not None else None
    result["Observation"] = observation
    result["ConnectionId"] = guid(value["ConnectionId"], "trace connection") if value.get("ConnectionId") is not None else None
    quota = value.get("Quota")
    if quota is not None:
        quota = obj(quota, "quota window")
        quota = {"StartMilliseconds": integer(quota.get("StartMilliseconds"), "quota start"),
                 "Count": integer(quota.get("Count"), "quota count", 1, 8), "Allowed": quota.get("Allowed")}
        require(type(quota["Allowed"]) is bool and 0 <= result["Milliseconds"] - quota["StartMilliseconds"] < 1000,
                "Quota stamp is outside its actual window")
    result["Quota"] = quota
    return result


def endpoint(report, role):
    report = obj(report, role + " report")
    require(type(report.get("SchemaVersion")) is int and report["SchemaVersion"] == 2,
            "SchemaVersion 2 is required; old reports lack pairing evidence")
    require(report.get("NetworkRole") == role, "Wrong report role")
    for key in ("Saved", "FullNet02Accepted", "ReconnectVerified", "ReturnedToTitleObserved", "TitleSessionCleared"):
        require(report.get(key) is False, "Report cannot prove active basic probes: " + key)
    require(report.get("Failure", "missing") is None and report.get("TargetIsSynthetic") is True,
            "Failed or non-synthetic report")
    require(report.get("InventoryReferencesUnchanged") is True, "Inventory reference evidence missing")
    require(report.get("ObservationOnly") is (role == "Host"), "Observation role differs")
    for before, after in (("BaselineInventorySha256", "CurrentInventorySha256"),
                          ("BaselineVisibleMachineRawSha256", "CurrentVisibleMachineRawSha256")):
        require(digest(report.get(before), before) == digest(report.get(after), after), "Local values changed: " + before)
    integer(report.get("BaselineVisibleMachineCount"), "visible machine count")
    baseline = obj(report.get("Baseline"), "episode baseline")
    current = obj(report.get("Protocol"), "current protocol")
    for key in ("Active", "ProbeConfigured", "ProbeAllowed"):
        require(current.get(key) is True, "Protocol is not active/allowed: " + key)
    require(current.get("IsHost") is (role == "Host") and current.get("Faulted") is False
            and current.get("WritesEnabled") is False, "Protocol role/fault/write boundary differs")
    for key in ("RunId", "SessionId"):
        require(guid(current.get(key), key) == guid(baseline.get(key), "baseline " + key), "Protocol context changed: " + key)
    for key in ("WorldGeneration", "DisconnectGeneration", "QueueRejected", "Evicted"):
        require(integer(current.get(key), key) == integer(baseline.get(key), "baseline " + key), "Local evidence context changed: " + key)
    require(current["QueueRejected"] == current["Evicted"] == 0, "Incomplete queue/trace cannot prove the full basic interval")
    require(integer(current.get("QueueCount"), "queue count") == 0, "Pending work is not complete evidence")
    local_id = identity(current.get("LocalPlayerId"), "local player")
    require(local_id == identity(baseline.get("LocalPlayerId"), "baseline local player"), "Local player changed")
    peers = [peer(p) for p in array(current.get("Peers"), "current peers")]
    initial = [peer(p) for p in array(baseline.get("Peers"), "baseline peers")]
    require(len(peers) == 1 and peers == initial and peers[0]["PlayerId"] != local_id
            and peers[0]["IsHost"] is (role == "Client"), "Peer role/connection/handshake changed or is ambiguous")
    traces = [trace(t) for t in array(current.get("Trace"), "trace")]
    require(0 < len(traces) <= 256 and [t["Sequence"] for t in traces] == list(range(1, len(traces) + 1)),
            "Trace is missing, duplicated or out of order")
    require(integer(current.get("LastSequence"), "last sequence") == len(traces), "Trace tail does not reach last sequence")
    require([t["Milliseconds"] for t in traces] == sorted(t["Milliseconds"] for t in traces), "Trace clock regressed")
    start = integer(baseline.get("LastSequence"), "baseline sequence", 1, len(traces))
    owner = integer(baseline.get("OwnerThreadId"), "owner thread", 1)
    result = {"report": report, "baseline": baseline, "protocol": current, "peer": peers[0], "traces": traces,
              "after": traces[start:], "local": local_id, "start": start, "owner": owner}
    for t in result["after"]:
        require(t["Kind"] in ("Processed", "SendAttempt", "ProbeAttempt"), "Interrupted/suppressed/stale evidence interval")
        local_event(result, t)
    validate_local_quota(traces)
    return result


def validate_local_quota(traces):
    """Reconstruct each local peer's admitted-read count from a complete, un-evicted tail.

    DisconnectGeneration is deliberately not a quota key: another peer disconnecting
    invalidates queued events, but does not reset an existing peer's read allowance.
    """
    windows = {}
    for t in traces:
        quota = t["Quota"]
        if quota is None:
            continue
        observation = t["Observation"]
        require(t["Kind"] == "Processed" and observation is not None
                and observation["BodyReadAttempted"] is quota["Allowed"], "Quota stamp is not an actual processed read boundary")
        key = (t["WorldGeneration"], identity(t.get("PeerId"), "quota peer"), guid(t.get("ConnectionId"), "quota connection"))
        previous = windows.get(key)
        if previous is None or quota["StartMilliseconds"] != previous["StartMilliseconds"]:
            require(previous is None or quota["StartMilliseconds"] >= previous["StartMilliseconds"] + 1000,
                    "Local quota window changed before its fixed boundary")
            require(quota["Allowed"] is True and quota["Count"] == 1, "Local quota window does not begin with its first read")
        elif quota["Allowed"]:
            require(quota["Count"] == previous["Count"] + 1, "An allowed local quota count was repeated or skipped")
        else:
            require(previous["Count"] == quota["Count"] == 8 and observation["Kind"] == "RateLimited"
                    and observation["RequestId"] is None and observation["Reason"] is None,
                    "Local quota drop preceded exhaustion or retained read metadata")
        windows[key] = quota


def local_event(end, t):
    b = end["baseline"]
    require(t["WorldGeneration"] == b["WorldGeneration"] and t["DisconnectGeneration"] == b["DisconnectGeneration"]
            and t["ThreadId"] == end["owner"] and type(t.get("PeerId")) is int and t["PeerId"] == end["peer"]["PlayerId"]
            and t["ConnectionId"] == end["peer"]["ConnectionId"], "Event belongs to another local connection/world/thread")


def processed(end, message_type, kind):
    return [t for t in end["after"] if t["Kind"] == "Processed" and t.get("MessageType") == message_type
            and t["Observation"] is not None and t["Observation"]["Kind"] == kind]


def adjacent_send(end, t, message_type):
    require(t["Sequence"] < len(end["traces"]), "Receive/probe has no subsequent send attempt")
    following = end["traces"][t["Sequence"]]
    local_event(end, following)
    require(following["Kind"] == "SendAttempt" and following.get("MessageType") == message_type
            and following["Observation"] is None and following["Quota"] is None, "Send attempt is not adjacent to its cause")


def validate_pair(host, client):
    """Return a basic-evidence summary, or reject incomplete/inconsistent observations.

    No network authentication is inferred. In particular the unread ninth body has no
    RequestId: its evidence is the complete same-peer quota window, not a fabricated ID.
    """
    h, c = endpoint(host, "Host"), endpoint(client, "Client")
    for end in (h, c):
        report = end["report"]
        integer(report.get("ProcessId"), "process ID", 1)
        integer(report.get("Episode"), "episode", 1)
        guid(report.get("HarnessRunId"), "harness run")
    require(host["ProcessId"] != client["ProcessId"] and guid(host["HarnessRunId"], "host run") != guid(client["HarnessRunId"], "client run")
            and guid(h["protocol"]["RunId"], "host protocol run") != guid(c["protocol"]["RunId"], "client protocol run"), "Reports are not two distinct processes/runs")
    require(integer(host.get("ExpectedFixtureId"), "host fixture", 1, 2**64 - 1)
            == integer(client.get("ExpectedFixtureId"), "client fixture", 1, 2**64 - 1), "Independent world IDs differ")
    require(digest(host.get("ProductionDllSha256"), "host DLL") == digest(client.get("ProductionDllSha256"), "client DLL"), "Production builds differ")
    require(guid(h["protocol"]["SessionId"], "host session") == guid(c["protocol"]["SessionId"], "client session")
            and h["local"] == c["peer"]["PlayerId"] and c["local"] == h["peer"]["PlayerId"], "Session/player pairing differs")
    require(host.get("BasicProbesCompleted") is False and type(host.get("Step")) is int and host["Step"] == 0
            and host.get("ProbeCases") == [] and host.get("BurstRequestIds") == [], "Host report is not a passive observer")
    require(client.get("BasicProbesCompleted") is True and type(client.get("Step")) is int and client["Step"] == 6
            and client.get("Passed") == [*PROBES, "BurstEightResponsesCandidate"], "Client basic candidate is incomplete")

    # A host peer's Challenge is locally generated, so compare the actual Hello observation,
    # never the two endpoint peer snapshots' Challenge or ConnectionId values.
    challenge = c["peer"]["Challenge"]
    handshakes = []
    for end, message_type, kind in ((h, HELLO, "HelloAccepted"), (c, WELCOME, "WelcomeAccepted")):
        matches = [t for t in end["traces"][:end["start"]] if t["Kind"] == "Processed" and t.get("MessageType") == message_type
                   and t["Observation"] is not None and t["Observation"]["Kind"] == kind
                   and t["Observation"]["Challenge"] == challenge]
        require(bool(matches), "Matching real hello/welcome observation is missing")
        for t in matches:
            local_event(end, t)
            require(t["Observation"]["BodyReadAttempted"] is True and t["Quota"] is not None
                    and t["Quota"]["Allowed"] is True, "Handshake was not actually read within quota")
            if end is h:
                adjacent_send(end, t, WELCOME)
        handshakes.append(matches)
    require(len(handshakes[1]) == 1, "Ambiguous accepted welcome")
    hello_sends = [t for t in c["traces"][:c["start"]] if t["Kind"] == "SendAttempt" and t.get("MessageType") == HELLO
                   and t["Sequence"] < handshakes[1][0]["Sequence"]]
    require(bool(hello_sends), "Client welcome has no preceding actual hello send attempt")
    for t in hello_sends:
        local_event(c, t)
        require(t["Observation"] is None and t["Quota"] is None, "Hello send is confused with an inbound read")

    cases = array(client.get("ProbeCases"), "probe cases")
    require(len(cases) == 5, "Five ordered probe cases required")
    case_ids, cuts = [], []
    for index, case in enumerate(cases):
        case = obj(case, "probe case")
        require(enum(case.get("Kind"), PROBES, "probe kind") == PROBES[index]
                and enum(case.get("Expected"), REASONS, "expected rejection") == EXPECTED[index], "Probe order or expectation differs")
        case_ids.append(guid(case.get("RequestId"), "probe ID"))
        cuts.append(integer(case.get("SentAfterSequence"), "sent sequence", c["start"], len(c["traces"])))
    burst_ids = [guid(v, "burst ID") for v in array(client.get("BurstRequestIds"), "burst IDs")]
    require(len(burst_ids) == 9 and len(set(case_ids + burst_ids)) == 14, "Missing/reused probe or burst request identity")
    burst_cut = integer(client.get("BurstSentAfterSequence"), "burst send sequence", c["start"], len(c["traces"]))
    require(all(a < b for a, b in zip(cuts, cuts[1:] + [burst_cut])), "Probe send boundaries are not ordered")
    host_reads = processed(h, REQUEST, "RejectionProduced")
    client_reads = processed(c, REJECTED, "RejectionAccepted")
    require(len(host_reads) == len(client_reads) == 13, "Missing or extra rejection reads")
    require(len([t for t in c["after"] if t["Kind"] == "Processed"]) == 13, "Unexpected client traffic in evidence interval")
    for index, (start, stop) in enumerate(zip(cuts, cuts[1:] + [burst_cut])):
        section = [t for t in c["traces"] if start < t["Sequence"] <= stop]
        probes = [t for t in section if t["Kind"] == "ProbeAttempt" and t.get("MessageType") == REQUEST]
        responses = [t for t in section if t["Kind"] == "Processed"]
        require(len(probes) == len(responses) == 1, "Probe case interval contains missing/ambiguous sends or responses")
        adjacent_send(c, probes[0], REQUEST)
        require(responses[0]["Sequence"] > probes[0]["Sequence"] + 1, "Response preceded its actual send attempt")
        request_id = case_ids[index] if index < 4 else None
        for t in (responses[0], host_reads[index]):
            obs = t["Observation"]
            require(obs["BodyReadAttempted"] is True and obs["RequestId"] == request_id and obs["Reason"] == EXPECTED[index]
                    and t["Quota"] is not None and t["Quota"]["Allowed"] is True, "Probe rejection correlation/read differs")
        adjacent_send(h, host_reads[index], REJECTED)

    client_burst = [t for t in c["after"] if t["Sequence"] > burst_cut]
    attempts = [t for t in client_burst if t["Kind"] == "ProbeAttempt" and t.get("MessageType") == REQUEST]
    require(len(attempts) == 9 and len([t for t in c["after"] if t["Kind"] == "SendAttempt"]) == 14
            and len([t for t in c["after"] if t["Kind"] == "ProbeAttempt"]) == 14, "Nine burst send attempts are not complete")
    for t in attempts:
        adjacent_send(c, t, REQUEST)
    burst_responses = [t for t in client_reads if t["Sequence"] > burst_cut]
    require(len(burst_responses) == 8 and all(t["Sequence"] > attempts[-1]["Sequence"] + 1 for t in burst_responses),
            "Burst responses do not follow all nine send attempts")
    burst_reads = host_reads[5:]
    ids = [t["Observation"]["RequestId"] for t in burst_reads]
    response_ids = [t["Observation"]["RequestId"] for t in burst_responses]
    require(len(set(ids)) == len(set(response_ids)) == 8 and set(ids) == set(response_ids) and set(ids) <= set(burst_ids),
            "Host/client burst rejection IDs are not the same eight unique requests")
    for t in burst_reads + burst_responses:
        require(t["Observation"]["BodyReadAttempted"] is True and t["Observation"]["Reason"] == "WritesDisabled"
                and t["Quota"] is not None and t["Quota"]["Allowed"] is True, "Burst rejection was not actually read")
    for t in burst_reads:
        adjacent_send(h, t, REJECTED)
    starts = {t["Quota"]["StartMilliseconds"] for t in burst_reads}
    require(len(starts) == 1 and [t["Quota"]["Count"] for t in burst_reads] == list(range(1, 9)),
            "Burst did not consume counts one through eight in one actual host quota window")
    window = starts.pop()
    # Account for all processed traffic in the selected host window, including malformed/Hello;
    # picking eight convenient events from a larger or split window is not valid evidence.
    window_reads = [t for t in h["traces"] if t["Kind"] == "Processed" and t["Quota"] is not None
                    and t["Quota"]["StartMilliseconds"] == window]
    require(len(window_reads) == 9 and window_reads[:8] == burst_reads, "Quota window includes other reads or missing/extra drops")
    drop = window_reads[-1]
    local_event(h, drop)
    require(drop.get("MessageType") == REQUEST and drop["Observation"] is not None
            and drop["Observation"]["Kind"] == "RateLimited" and drop["Observation"]["BodyReadAttempted"] is False
            and drop["Observation"]["RequestId"] is None and drop["Observation"]["Reason"] is None
            and drop["Quota"] == {"StartMilliseconds": window, "Count": 8, "Allowed": False},
            "Ninth event is not a proven pre-body-read quota rejection")
    require(len([t for t in h["after"] if t["Kind"] == "Processed"]) == 14
            and len([t for t in h["after"] if t["Kind"] == "SendAttempt"]) == 13, "Host interval is incomplete or contains unrelated traffic")
    return {"SchemaVersion": 1, "BasicProbeEvidenceVerified": True, "FullNet02Accepted": False,
            "TargetIsSynthetic": True, "ExpectedFixtureId": host["ExpectedFixtureId"],
            "ProductionDllSha256": digest(host["ProductionDllSha256"], "DLL"), "ProbeCasesVerified": 5,
            "BurstHostBodyReads": 8, "BurstHostPreReadQuotaDrops": 1,
            "HostVisibleMachineCount": host["BaselineVisibleMachineCount"], "ClientVisibleMachineCount": client["BaselineVisibleMachineCount"],
            "Limitations": ["Observed metadata is not sender authentication", "Synthetic target is not real-machine authorization",
                            "No lifecycle, queue-capacity, remote inventory or full NET-02 acceptance"]}


def read_report(path):
    # Bound the read itself, including a file that grows while being read. Checking length only
    # after read_bytes would still allocate an arbitrary input before rejecting it.
    with path.open("rb") as source:
        data = source.read(MAX_REPORT_BYTES + 1)
    require(len(data) <= MAX_REPORT_BYTES, "Report exceeds 2 MiB")
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "Duplicate JSON field")
            result[key] = value
        return result
    def finite(_value):
        raise ValueError("Non-finite JSON number")
    def finite_float(value):
        parsed = float(value)
        require(math.isfinite(parsed), "JSON numeric overflow")
        return parsed
    return json.loads(data.decode("utf-8-sig"), object_pairs_hook=unique, parse_constant=finite,
                      parse_float=finite_float), hashlib.sha256(data).hexdigest()


def validate_output(path, inputs, root=ROOT):
    resolved = path.resolve()
    local = (root / ".local").resolve()
    require(local == root.resolve() / ".local", "Workspace .local must not resolve outside its own directory")
    require(resolved != local and resolved.is_relative_to(local), "Output must be a file beneath workspace .local")
    require(all(resolved != source.resolve() for source in inputs), "Output cannot overwrite an input report")
    require(not resolved.exists() or (resolved.is_file() and resolved.stat().st_nlink == 1), "Output must not be a linked/shared or directory target")
    return resolved


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", type=Path, required=True)
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--output", type=Path, help="Optional summary under workspace .local; input reports are never modified")
    args = parser.parse_args()
    try:
        # Resolve the current workspace at call time; a default parameter captured at import
        # time would incorrectly reject a valid independent workspace used by another caller.
        output = validate_output(args.output, (args.host, args.client), root=ROOT) if args.output else None
        host, host_hash = read_report(args.host)
        client, client_hash = read_report(args.client)
        result = validate_pair(host, client)
        result.update(HostReportSha256=host_hash, ClientReportSha256=client_hash)
        text = json.dumps(result, ensure_ascii=False, indent=2) + "\n"
        if output:
            output.parent.mkdir(parents=True, exist_ok=True)
            output.write_text(text, encoding="utf-8")
        print(text, end="")
        return 0
    except (OSError, ValueError, TypeError, KeyError, OverflowError):
        # Avoid echoing private paths/raw fields in CLI rejection output. Unit callers receive the
        # specific invariant from ValueError; malformed/missing records never produce a success file.
        print("REJECT: incomplete or inconsistent schema-2 basic probe evidence; inspect both reports and the documented contract.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
