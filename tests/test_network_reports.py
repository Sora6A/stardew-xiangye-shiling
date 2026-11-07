"""人工构造的 NET-02 双端报告回归；不启动游戏，不使用实机证据或个人文件。"""

import copy
import contextlib
import io
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import uuid


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import check_network_reports as reports


HELLO = "XYS/Hello/1"
WELCOME = "XYS/Welcome/1"
REQUEST = "XYS/Request/1"
REJECTED = "XYS/Rejected/1"
CASE_NAMES = ("Start", "Collect", "ActorMismatch", "WrongSession", "Malformed")
CASE_REASONS = ("WritesDisabled", "WritesDisabled", "ActorMismatch", "WrongSession", "InvalidRequest")


def guid(number):
    """固定人工标识，避免随机 fixture 掩盖关联关系。"""
    return str(uuid.UUID(int=number))


def synthetic_pair():
    """手工描述允许的消息轨迹，仅用于检验校验器，绝非真实联网通过记录。"""
    session = guid(1)
    challenge = guid(2)
    host_id, client_id = 1001, 2002

    def report(role, player, peer, thread, generation, disconnect, number):
        peer_status = {"PlayerId": peer, "ConnectionId": guid(number + 2),
                       "IsHost": role == "Client", "Ready": True,
                       "Challenge": challenge if role == "Client" else guid(number + 3)}
        protocol = {
            "RunId": guid(number + 1), "ProbeConfigured": True, "ProbeAllowed": True,
            "Active": True, "IsHost": role == "Host", "Faulted": False, "WritesEnabled": False,
            "LocalPlayerId": player, "SessionId": session,
            "WorldGeneration": generation, "DisconnectGeneration": disconnect,
            "QueueCount": 0, "QueueRejected": 0, "LastSequence": 0, "Evicted": 0,
            "Peers": [peer_status], "Trace": [],
        }
        baseline = {
            "RunId": protocol["RunId"], "SessionId": session, "WorldGeneration": generation,
            "DisconnectGeneration": disconnect, "LocalPlayerId": player, "OwnerThreadId": thread,
            "QueueRejected": 0, "Evicted": 0, "LastSequence": 3,
            "Peers": copy.deepcopy(protocol["Peers"]),
        }
        return {
            "SchemaVersion": 2, "HarnessRunId": guid(number), "ProcessId": number,
            "Episode": 1, "NetworkRole": role, "ProductionDllSha256": "a" * 64,
            "ExpectedFixtureId": 900001, "BaselineVisibleMachineCount": 0,
            "TargetIsSynthetic": True, "BasicProbesCompleted": role == "Client",
            "FullNet02Accepted": False, "ReconnectVerified": False,
            "ReturnedToTitleObserved": False, "TitleSessionCleared": False,
            "Saved": False, "Failure": None, "Step": 6 if role == "Client" else 0,
            "Passed": list(CASE_NAMES) + ["BurstEightResponsesCandidate"] if role == "Client" else [],
            "ProbeCases": [], "BurstRequestIds": [], "BurstSentAfterSequence": 0,
            "Baseline": baseline, "BaselineInventorySha256": "b" * 64,
            "BaselineVisibleMachineRawSha256": "c" * 64,
            "CurrentInventorySha256": "b" * 64, "CurrentVisibleMachineRawSha256": "c" * 64,
            "InventoryReferencesUnchanged": True, "ObservationOnly": role == "Host",
            "Protocol": protocol,
            "Evidence": "Artificial unit-test fixture; no game or network execution.",
        }

    host = report("Host", host_id, client_id, 31, 3, 0, 5001)
    client = report("Client", client_id, host_id, 47, 7, 2, 6001)

    def append(value, kind, milliseconds, message=None, observation=None, quota=None):
        protocol = value["Protocol"]
        trace = {"Sequence": len(protocol["Trace"]) + 1, "Milliseconds": milliseconds,
                 "WorldGeneration": protocol["WorldGeneration"],
                 "DisconnectGeneration": protocol["DisconnectGeneration"],
                 "ThreadId": value["Baseline"]["OwnerThreadId"], "Kind": kind,
                 "PeerId": protocol["Peers"][0]["PlayerId"] if message else None,
                 "ConnectionId": protocol["Peers"][0]["ConnectionId"] if message else None,
                 "MessageType": message, "Observation": observation, "Quota": quota}
        protocol["Trace"].append(trace)
        protocol["LastSequence"] = trace["Sequence"]
        return trace

    def observation(kind, request=None, reason=None, handshake=None, read=True):
        return {"Kind": kind, "BodyReadAttempted": read, "RequestId": request,
                "Reason": reason, "Challenge": handshake}

    def quota(start, count, allowed=True):
        return {"StartMilliseconds": start, "Count": count, "Allowed": allowed}

    append(host, "Reset", 0)
    append(host, "Processed", 100, HELLO,
           observation("HelloAccepted", handshake=challenge), quota(0, 1))
    append(host, "SendAttempt", 100, WELCOME)
    append(client, "Reset", 0)
    append(client, "SendAttempt", 50, HELLO)
    append(client, "Processed", 120, WELCOME,
           observation("WelcomeAccepted", handshake=challenge), quota(0, 1))

    for index, (kind, reason) in enumerate(zip(CASE_NAMES, CASE_REASONS)):
        request = guid(100 + index)
        client["ProbeCases"].append({"Kind": kind, "RequestId": request, "Expected": reason,
                                     "SentAfterSequence": client["Protocol"]["LastSequence"]})
        # 本地时钟不做跨进程比较；两端仅各自单调，与真实 transport 的含义相同。
        client_time, host_time = 2000 + index * 1400, 2100 + index * 1400
        append(client, "ProbeAttempt", client_time, REQUEST)
        append(client, "SendAttempt", client_time, REQUEST)
        observed_id = None if kind == "Malformed" else request
        append(host, "Processed", host_time, REQUEST,
               observation("RejectionProduced", observed_id, reason), quota(host_time, 1))
        append(host, "SendAttempt", host_time, REJECTED)
        append(client, "Processed", client_time + 150, REJECTED,
               observation("RejectionAccepted", observed_id, reason), quota(client_time + 150, 1))

    client["BurstSentAfterSequence"] = client["Protocol"]["LastSequence"]
    client["BurstRequestIds"] = [guid(200 + index) for index in range(9)]
    for request in client["BurstRequestIds"]:
        append(client, "ProbeAttempt", 11000, REQUEST)
        append(client, "SendAttempt", 11000, REQUEST)
    for count, request in enumerate(client["BurstRequestIds"][:8], 1):
        append(host, "Processed", 11200, REQUEST,
               observation("RejectionProduced", request, "WritesDisabled"), quota(11200, count))
        append(host, "SendAttempt", 11200, REJECTED)
        append(client, "Processed", 11300, REJECTED,
               observation("RejectionAccepted", request, "WritesDisabled"), quota(11300, count))
    append(host, "Processed", 11200, REQUEST,
           observation("RateLimited", read=False), quota(11200, 8, False))
    return host, client


class NetworkReportPairTests(unittest.TestCase):
    def setUp(self):
        self.host, self.client = synthetic_pair()

    def rejected(self):
        with self.assertRaises(ValueError):
            reports.validate_pair(self.host, self.client)

    def host_requests(self):
        return [trace for trace in self.host["Protocol"]["Trace"]
                if trace["Kind"] == "Processed" and trace["MessageType"] == REQUEST]

    def client_responses(self):
        return [trace for trace in self.client["Protocol"]["Trace"]
                if trace["Kind"] == "Processed" and trace["MessageType"] == REJECTED]

    def test_artificial_complete_pair_only_verifies_basic_evidence_without_mutating_inputs(self):
        before = copy.deepcopy((self.host, self.client))
        result = reports.validate_pair(self.host, self.client)
        self.assertIs(result["BasicProbeEvidenceVerified"], True)
        self.assertIs(result["FullNet02Accepted"], False)
        self.assertEqual(before, (self.host, self.client))

    def test_local_thread_generation_and_connection_ids_need_not_match_between_processes(self):
        self.assertNotEqual(self.host["Baseline"]["OwnerThreadId"], self.client["Baseline"]["OwnerThreadId"])
        self.assertNotEqual(self.host["Baseline"]["WorldGeneration"], self.client["Baseline"]["WorldGeneration"])
        self.assertNotEqual(self.host["Protocol"]["Peers"][0]["ConnectionId"],
                            self.client["Protocol"]["Peers"][0]["ConnectionId"])
        self.assertIs(reports.validate_pair(self.host, self.client)["BasicProbeEvidenceVerified"], True)

    def test_each_end_local_fingerprints_and_visible_machine_count_may_differ(self):
        self.client.update(BaselineInventorySha256="d" * 64, CurrentInventorySha256="d" * 64,
                           BaselineVisibleMachineRawSha256="e" * 64, CurrentVisibleMachineRawSha256="e" * 64,
                           BaselineVisibleMachineCount=2)
        self.assertIs(reports.validate_pair(self.host, self.client)["BasicProbeEvidenceVerified"], True)

    def test_additional_fields_do_not_make_known_evidence_optional(self):
        self.host["AdditionalSyntheticNote"] = "Artificial fixture"
        self.client["Protocol"]["AdditionalSyntheticNumber"] = 0.5
        self.assertIs(reports.validate_pair(self.host, self.client)["BasicProbeEvidenceVerified"], True)
        del self.client["CurrentInventorySha256"]
        self.rejected()

    def test_legacy_or_missing_schema_is_insufficient_evidence(self):
        for field_value in (1, None, True, "2"):
            with self.subTest(value=field_value):
                self.host, self.client = synthetic_pair()
                self.client["SchemaVersion"] = field_value
                self.rejected()
        del self.client["SchemaVersion"]
        self.rejected()

    def test_known_booleans_and_integer_fields_do_not_accept_lookalike_types(self):
        changes = (("SchemaVersion", 2.0), ("Episode", True), ("ProcessId", "6001"),
                   ("BasicProbesCompleted", 1), ("Saved", 0), ("InventoryReferencesUnchanged", 1))
        for field, value in changes:
            with self.subTest(field=field, value=value):
                self.host, self.client = synthetic_pair()
                self.client[field] = value
                self.rejected()
        # 补充类型防回退；此两项加入时严格类型修正已存在，不作为独立修前红证据。
        for value in (False, 0.0):
            with self.subTest(host_step=value):
                self.host, self.client = synthetic_pair()
                self.host["Step"] = value
                self.rejected()

    def test_exact_enum_names_or_csharp_ordinals_are_accepted_but_unknown_values_are_rejected(self):
        trace_names = ("Reset", "Processed", "StaleQueue", "SendAttempt", "SendSuppressed", "ProbeAttempt", "Faulted")
        receive_names = ("Ignored", "RateLimited", "InvalidMessage", "HelloAccepted", "WelcomeAccepted",
                         "RejectionProduced", "RejectionAccepted")
        reason_names = ("WritesDisabled", "HandshakeRequired", "WrongSession", "ActorMismatch", "InvalidRequest")
        for value in (self.host, self.client):
            for trace in value["Protocol"]["Trace"]:
                trace["Kind"] = trace_names.index(trace["Kind"])
                if trace["Observation"] is not None:
                    observation = trace["Observation"]
                    observation["Kind"] = receive_names.index(observation["Kind"])
                    if observation["Reason"] is not None:
                        observation["Reason"] = reason_names.index(observation["Reason"])
        for probe in self.client["ProbeCases"]:
            probe["Kind"] = CASE_NAMES.index(probe["Kind"])
            probe["Expected"] = reason_names.index(probe["Expected"])
        self.assertIs(reports.validate_pair(self.host, self.client)["BasicProbeEvidenceVerified"], True)
        for value in (True, 999, "processed", "Unknown"):
            with self.subTest(value=value):
                self.host, self.client = synthetic_pair()
                self.client_responses()[0]["Kind"] = value
                self.rejected()

    def test_missing_baseline_or_current_fingerprint_does_not_inherit_defaults(self):
        for field in ("Baseline", "CurrentInventorySha256", "CurrentVisibleMachineRawSha256",
                      "InventoryReferencesUnchanged", "BurstSentAfterSequence"):
            with self.subTest(field=field):
                self.host, self.client = synthetic_pair()
                del self.client[field]
                self.rejected()

    def test_world_build_role_and_session_must_match(self):
        changes = (("ExpectedFixtureId", 900002), ("ProductionDllSha256", "d" * 64),
                   ("NetworkRole", "Host"), ("ExpectedFixtureId", 0))
        for field, value in changes:
            with self.subTest(field=field, value=value):
                self.host, self.client = synthetic_pair()
                self.client[field] = value
                self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["Protocol"]["SessionId"] = guid(990)
        self.client["Baseline"]["SessionId"] = guid(990)
        self.rejected()

    def test_both_reports_must_come_from_independent_runs_and_processes(self):
        for field in ("HarnessRunId", "ProcessId"):
            with self.subTest(field=field):
                self.host, self.client = synthetic_pair()
                self.client[field] = self.host[field]
                self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["Protocol"]["RunId"] = self.host["Protocol"]["RunId"]
        self.client["Baseline"]["RunId"] = self.host["Protocol"]["RunId"]
        self.rejected()

    def test_failed_saved_or_claimed_full_acceptance_reports_are_rejected(self):
        for field, value in (("Failure", "synthetic failure"), ("Saved", True),
                             ("FullNet02Accepted", True), ("ReconnectVerified", True),
                             ("ReturnedToTitleObserved", True), ("InventoryReferencesUnchanged", False)):
            with self.subTest(field=field):
                self.host, self.client = synthetic_pair()
                self.host[field] = value
                self.rejected()

    def test_protocol_gate_fault_or_missing_ready_peer_is_rejected(self):
        for field, value in (("Active", False), ("Faulted", True), ("WritesEnabled", True),
                             ("ProbeConfigured", False), ("ProbeAllowed", False)):
            with self.subTest(field=field):
                self.host, self.client = synthetic_pair()
                self.client["Protocol"][field] = value
                self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["Protocol"]["Peers"][0]["Ready"] = False
        self.rejected()

    def test_readonly_hashes_are_compared_with_each_end_own_baseline(self):
        for role in ("host", "client"):
            for field in ("CurrentInventorySha256", "CurrentVisibleMachineRawSha256"):
                with self.subTest(role=role, field=field):
                    self.host, self.client = synthetic_pair()
                    getattr(self, role)[field] = "e" * 64
                    self.rejected()

    def test_connection_run_or_generation_change_cannot_be_spliced(self):
        for field, value in (("RunId", guid(991)), ("WorldGeneration", 99), ("DisconnectGeneration", 99)):
            with self.subTest(field=field):
                self.host, self.client = synthetic_pair()
                self.client["Protocol"][field] = value
                self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["Protocol"]["Peers"][0]["ConnectionId"] = guid(992)
        self.rejected()

    def test_duplicate_peers_or_trace_sequence_are_rejected(self):
        self.client["Protocol"]["Peers"].append(copy.deepcopy(self.client["Protocol"]["Peers"][0]))
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["Protocol"]["Trace"][4]["Sequence"] = self.client["Protocol"]["Trace"][3]["Sequence"]
        self.rejected()

    def test_trace_milliseconds_are_monotonic_and_sequence_has_no_gap(self):
        self.client["Protocol"]["Trace"][4]["Milliseconds"] = 1
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["Protocol"]["Trace"][4]["Sequence"] += 1
        self.rejected()

    def test_trace_loss_pending_queue_and_last_sequence_mismatch_are_rejected(self):
        for field, value in (("Evicted", 1), ("QueueRejected", 1), ("QueueCount", 1), ("LastSequence", 999)):
            with self.subTest(field=field):
                self.host, self.client = synthetic_pair()
                self.host["Protocol"][field] = value
                self.rejected()

    def test_matching_peer_and_actual_hello_welcome_challenge_are_required(self):
        self.host["Protocol"]["Trace"][1]["Observation"]["Challenge"] = guid(993)
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["Protocol"]["Trace"][2]["Observation"]["Challenge"] = guid(993)
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.host["Protocol"]["Peers"][0]["PlayerId"] = 3003
        self.host["Baseline"]["Peers"][0]["PlayerId"] = 3003
        self.rejected()

    def test_client_welcome_requires_a_preceding_actual_hello_send_attempt(self):
        """BUG-017: 正确challenge的Welcome不能替代本端实际Hello发送轨迹。"""
        self.client["Protocol"]["Trace"][1]["MessageType"] = REQUEST
        self.rejected()

    def test_client_candidate_flags_do_not_replace_host_rejection_evidence(self):
        self.host_requests()[0]["Observation"]["Kind"] = "InvalidMessage"
        self.rejected()

    def test_probe_list_requires_each_kind_expected_reason_and_unique_request(self):
        for mutation in (lambda cases: cases.pop(),
                         lambda cases: cases[1].update(Kind="Start"),
                         lambda cases: cases[2].update(Expected="WritesDisabled"),
                         lambda cases: cases[1].update(RequestId=cases[0]["RequestId"])):
            with self.subTest(mutation=mutation):
                self.host, self.client = synthetic_pair()
                mutation(self.client["ProbeCases"])
                self.rejected()

    def test_correct_reason_from_wrong_type_peer_thread_or_generation_does_not_pass(self):
        for field, value in (("MessageType", WELCOME), ("PeerId", 3003), ("ConnectionId", guid(999)), ("ThreadId", 99),
                             ("WorldGeneration", 99), ("DisconnectGeneration", 99)):
            with self.subTest(field=field):
                self.host, self.client = synthetic_pair()
                self.client_responses()[0][field] = value
                self.rejected()

    def test_response_must_be_read_and_accepted_after_its_actual_send(self):
        self.client_responses()[0]["Observation"]["BodyReadAttempted"] = False
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.client_responses()[0]["Observation"]["Kind"] = "RejectionProduced"
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["ProbeCases"][0]["SentAfterSequence"] = self.client_responses()[0]["Sequence"]
        self.rejected()

    def test_send_attempts_are_required_and_send_suppression_is_not_delivery_evidence(self):
        for role, message in (("host", REJECTED), ("client", REQUEST)):
            with self.subTest(role=role):
                self.host, self.client = synthetic_pair()
                trace = next(trace for trace in getattr(self, role)["Protocol"]["Trace"]
                             if trace["Kind"] == "SendAttempt" and trace["MessageType"] == message)
                trace["Kind"] = "SendSuppressed"
                self.rejected()

    def test_malformed_null_id_needs_both_end_matching_invalid_request_evidence(self):
        self.host_requests()[4]["Observation"]["Reason"] = "WritesDisabled"
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.client_responses()[4]["Observation"]["RequestId"] = self.client["ProbeCases"][4]["RequestId"]
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.client["ProbeCases"][4]["SentAfterSequence"] = self.client_responses()[4]["Sequence"]
        self.rejected()

    def test_burst_requires_nine_unique_new_requests_and_eight_distinct_responses(self):
        for mutation in (lambda ids: ids.pop(), lambda ids: ids.__setitem__(8, ids[0]),
                         lambda ids: ids.__setitem__(8, self.client["ProbeCases"][0]["RequestId"])):
            with self.subTest(mutation=mutation):
                self.host, self.client = synthetic_pair()
                mutation(self.client["BurstRequestIds"])
                self.rejected()
        self.host, self.client = synthetic_pair()
        self.client_responses()[-1]["Observation"]["RequestId"] = self.client_responses()[-2]["Observation"]["RequestId"]
        self.rejected()

    def test_client_eight_responses_without_host_ninth_unread_drop_are_insufficient(self):
        self.host_requests()[-1]["Observation"] = None
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.host_requests()[-1]["Observation"]["BodyReadAttempted"] = True
        self.rejected()

    def test_extra_burst_response_or_drop_cannot_be_silently_ignored(self):
        for role in ("host", "client"):
            with self.subTest(role=role):
                self.host, self.client = synthetic_pair()
                protocol = getattr(self, role)["Protocol"]
                extra = copy.deepcopy(protocol["Trace"][-1])
                extra["Sequence"] += 1
                protocol["Trace"].append(extra)
                protocol["LastSequence"] += 1
                self.rejected()

    def test_rate_limited_drop_must_be_for_same_peer_type_generation_and_thread(self):
        for field, value in (("PeerId", 3003), ("ConnectionId", guid(999)), ("MessageType", HELLO), ("WorldGeneration", 99),
                             ("DisconnectGeneration", 99), ("ThreadId", 99)):
            with self.subTest(field=field):
                self.host, self.client = synthetic_pair()
                self.host_requests()[-1][field] = value
                self.rejected()

    def test_quota_window_is_explicit_and_cannot_be_inferred_from_nearby_timestamps(self):
        for mutation in (lambda events: events[-1].update(Quota=None),
                         lambda events: events[-1]["Quota"].update(StartMilliseconds=11201),
                         lambda events: events[-1]["Quota"].update(Allowed=True),
                         lambda events: events[-1]["Quota"].update(Count=7),
                         lambda events: events[-2]["Quota"].update(Count=7)):
            with self.subTest(mutation=mutation):
                self.host, self.client = synthetic_pair()
                mutation(self.host_requests())
                self.rejected()

    def test_eight_reads_must_fit_reported_fixed_window_and_conserve_quota_count(self):
        self.host_requests()[-1]["Milliseconds"] = 12200
        self.rejected()
        self.host, self.client = synthetic_pair()
        self.host_requests()[5]["Quota"]["Count"] = 2
        self.rejected()

    def test_each_local_quota_window_must_consume_allowed_counts_once(self):
        """BUG-017: 客户端读取回执也使用额度，同窗重复Count=8不是可能的真实观测。"""
        for trace in self.client_responses():
            if trace["Sequence"] > self.client["BurstSentAfterSequence"]:
                trace["Quota"]["Count"] = 8
        self.rejected()


class NetworkReportCliTests(unittest.TestCase):
    def setUp(self):
        self.sandbox = tempfile.TemporaryDirectory()
        self.addCleanup(self.sandbox.cleanup)
        self.root = Path(self.sandbox.name)
        (self.root / ".local").mkdir()
        self.host_path = self.root / ".local/host.json"
        self.client_path = self.root / ".local/client.json"
        self.output_path = self.root / ".local/pair-audit.json"
        self.host, self.client = synthetic_pair()
        self.write_pair()

    def write_pair(self):
        for path, value in ((self.host_path, self.host), (self.client_path, self.client)):
            path.write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")

    def invoke(self, *extra):
        # 与真实CLI相同的解析/路径/读写入口；仅将根移到独立临时目录，绝不写正式.local。
        stdout, stderr = io.StringIO(), io.StringIO()
        args = ["check_network_reports.py", "--host", str(self.host_path), "--client", str(self.client_path),
                "--output", str(self.output_path), *extra]
        with patch.object(reports, "ROOT", self.root), patch.object(sys, "argv", args), \
                contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            try:
                result = reports.main()
            except SystemExit as error:
                result = error.code
        return SimpleNamespace(returncode=result, stdout=stdout.getvalue(), stderr=stderr.getvalue())

    def test_cli_success_writes_limited_result_and_preserves_both_report_bytes(self):
        original = (self.host_path.read_bytes(), self.client_path.read_bytes())
        completed = self.invoke()
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        result = json.loads(self.output_path.read_text(encoding="utf-8"))
        self.assertIs(result["BasicProbeEvidenceVerified"], True)
        self.assertIs(result["FullNet02Accepted"], False)
        self.assertEqual(original, (self.host_path.read_bytes(), self.client_path.read_bytes()))

    def test_cli_rejects_duplicate_json_keys_at_any_depth_without_overwriting_old_audit(self):
        original = b"previous artificial audit; preserve on failure\n"
        self.output_path.write_bytes(original)
        for old, duplicate in (("\"SchemaVersion\": 2", "\"SchemaVersion\": 1, \"SchemaVersion\": 2"),
                               ("\"WritesEnabled\": false", "\"WritesEnabled\": true, \"WritesEnabled\": false")):
            with self.subTest(field=old):
                self.write_pair()
                text = self.client_path.read_text(encoding="utf-8").replace(old, duplicate, 1)
                self.client_path.write_text(text, encoding="utf-8")
                completed = self.invoke()
                self.assertNotEqual(0, completed.returncode)
                self.assertEqual(original, self.output_path.read_bytes())

    def test_cli_rejects_non_finite_numbers_and_json_numeric_overflow(self):
        for token in ("NaN", "Infinity", "-Infinity", "1e999"):
            with self.subTest(token=token):
                self.write_pair()
                text = self.host_path.read_text(encoding="utf-8").replace(
                    "\"Episode\": 1", "\"Episode\": " + token, 1)
                self.host_path.write_text(text, encoding="utf-8")
                completed = self.invoke()
                self.assertNotEqual(0, completed.returncode)
                self.assertFalse(self.output_path.exists())

    def test_parser_rejects_non_finite_numbers_even_in_ignored_additional_fields(self):
        for token in ("NaN", "Infinity", "-Infinity", "1e999"):
            with self.subTest(token=token):
                self.write_pair()
                text = self.host_path.read_text(encoding="utf-8")[:-1] + ", \"AdditionalSyntheticNumber\": " + token + "}"
                self.host_path.write_text(text, encoding="utf-8")
                with self.assertRaises(ValueError):
                    reports.read_report(self.host_path)
                completed = self.invoke()
                self.assertNotEqual(0, completed.returncode)
                self.assertFalse(self.output_path.exists())

    def test_cli_rejects_non_object_invalid_json_and_legacy_schema(self):
        for text in ("[]", "null", "{incomplete", json.dumps({**self.client, "SchemaVersion": 1})):
            with self.subTest(text=text[:40]):
                self.client_path.write_text(text, encoding="utf-8")
                completed = self.invoke()
                self.assertNotEqual(0, completed.returncode)
                self.assertFalse(self.output_path.exists())

    def test_cli_output_cannot_replace_either_source_report(self):
        original = (self.host_path.read_bytes(), self.client_path.read_bytes())
        for output in (self.host_path, self.client_path):
            with self.subTest(output=output.name):
                completed = self.invoke("--output", str(output))
                self.assertNotEqual(0, completed.returncode)
                self.assertEqual(original, (self.host_path.read_bytes(), self.client_path.read_bytes()))

    def test_cli_output_rejects_local_root_workspace_and_escape(self):
        for output in (self.root, self.root / ".local", self.root / ".local/../outside.json",
                       self.root / ".local-other/audit.json", self.root.parent / "outside-report.json"):
            with self.subTest(output=str(output)):
                completed = self.invoke("--output", str(output))
                self.assertNotEqual(0, completed.returncode)
                self.assertFalse(self.output_path.exists())

    def test_cli_rejects_reports_over_two_mib_without_changing_sources_or_output(self):
        self.host_path.write_bytes(b" " * (2 * 1024 * 1024 + 1))
        original = self.host_path.read_bytes()
        completed = self.invoke()
        self.assertNotEqual(0, completed.returncode)
        self.assertFalse(self.output_path.exists())
        self.assertEqual(original, self.host_path.read_bytes())


if __name__ == "__main__":
    unittest.main()
