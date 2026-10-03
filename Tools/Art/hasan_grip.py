"""Offline CC0 MakeHuman finger corrective, without changing FOC's runtime rig.

The unchanged anatomical hand is the basis. Original upstream finger joints and
weights (discarded only when the 18-bone runtime rig was consolidated) author a
closed-grip blend shape. No replacement hand, procedural fingers or runtime
finger bones are created. Import from Blender; this module has no scene effects.

These are draft authored grips, not automatic visual acceptance. The weapon
attachment returned here aligns the existing kilic handle with this corrective;
it is deliberately separate from gameplay/persistent loadout truth.
"""
import json
import math
from mathutils import Matrix, Quaternion, Vector
from mathutils.kdtree import KDTree


def _average(vectors):
    values = list(vectors)
    return sum(values, Vector()) / len(values)


def _round3(v):
    return [round(float(value), 7) for value in v]


def _rotation_at(origin, axis, angle):
    return (Matrix.Translation(origin)
            @ Quaternion(axis, math.radians(angle)).to_matrix().to_4x4()
            @ Matrix.Translation(-origin))


def _segment_matrix(rest_head, rest_tail, posed_head, posed_tail):
    rotation = (rest_tail-rest_head).rotation_difference(posed_tail-posed_head)
    return (Matrix.Translation(posed_head) @ rotation.to_matrix().to_4x4()
            @ Matrix.Translation(-rest_head))


def _thumb_arc_angle(lengths, reach):
    """Choose two moderate hinges, rather than forcing a hooked terminal joint.

    Pure numeric helper: three preserved lengths, equal successive bend angles,
    and the original target distance fully define the planar chord length.
    Reject an unreachable target; do not silently clamp or shorten anatomy.
    """
    def chord(angle):
        x = lengths[0]+lengths[1]*math.cos(angle)+lengths[2]*math.cos(2*angle)
        y = lengths[1]*math.sin(angle)+lengths[2]*math.sin(2*angle)
        return math.hypot(x, y)

    lower, upper = 0.0, math.radians(65)
    if not chord(upper) <= reach <= chord(lower):
        raise ValueError('Thumb target cannot be reached within moderate hinge bounds')
    for _ in range(48):
        middle = (lower+upper)*.5
        if chord(middle) > reach:
            lower = middle
        else:
            upper = middle
    angle = (lower+upper)*.5
    if not math.radians(15) < angle <= math.radians(65):
        raise ValueError('Thumb opposition would be straight or over-flexed')
    return angle


class _GripAuthor:
    def __init__(self, donor, side):
        self.side = side
        self.points = donor.points
        rig = json.loads((donor.INPUT/'default.mhskel').read_text())
        source_weights = json.loads((donor.INPUT/'default_weights.mhw').read_text())['weights']
        self.weights = {}
        for name, entries in source_weights.items():
            for vi, weight in entries:
                self.weights.setdefault(vi, []).append((name, weight))

        def joint(name, end='head'):
            return _average(donor.points[i] for i in rig['joints'][rig['bones'][name][end]])

        self.wrist = joint('wrist.'+side)
        canonical_wrist = donor.bp['Hand_'+side]
        if (self.wrist-canonical_wrist).length > .00001:
            raise ValueError('Grip source and unchanged canonical wrist differ')
        index = joint('finger2-1.'+side)
        pinky = joint('finger5-1.'+side)
        self.width = (index-pinky).normalized()  # little finger -> index/thumb
        distal = (_average([index, pinky])-self.wrist).normalized()
        distal = (distal-self.width*distal.dot(self.width)).normalized()
        self.distal = distal
        # Mirroring exchanges handedness; palmward must point inward on both.
        self.palm = self.width.cross(distal).normalized() * (1 if side == 'R' else -1)
        knuckles = _average(joint('finger%d-1.%s' % (finger, side)) for finger in range(2, 6))
        # The rest phalanges already curve palmward. The cylindrical grip lies
        # slightly PROXIMAL to the knuckle row, not beyond it: placing it distally
        # would put the first finger joints through the handle after flexion.
        self.center = knuckles - distal*.011 + self.palm*.029

        self.transforms = {}
        for finger in range(2, 6):
            parent = Matrix.Identity(4)
            # A relaxed cylindrical grip, not a collapsed zero-radius fist.
            # Distal joints close further for shorter outer fingers.
            curls = (43, 67, 48) if finger in (2, 3) else (47, 72, 50)
            for phalanx, angle in enumerate(curls, 1):
                name = 'finger%d-%d.%s' % (finger, phalanx, side)
                head, tail = joint(name), joint(name, 'tail')
                axis = (tail-head).normalized().cross(self.palm).normalized()
                parent = parent @ _rotation_at(head, axis, angle)
                self.transforms[name] = parent

        # Thumb uses separate opposition, not the same curl as four fingers.
        # The former fixed terminal direction over-reached the first two bones,
        # then hooked the final joint by ~103 degrees. Distribute opposition as
        # a continuous three-segment arc while keeping the same root, lengths
        # and authored target. Source anatomy, skin weights and socket stay put.
        thumb_names = ['finger1-%d.%s' % (i, side) for i in range(1, 4)]
        heads = [joint(name) for name in thumb_names]
        tails = [joint(name, 'tail') for name in thumb_names]
        lengths = [(b-a).length for a, b in zip(heads, tails)]
        target = self.center+self.width*.024+self.palm*.023
        reach = target-heads[0]
        direction = reach.normalized()
        bend = _thumb_arc_angle(lengths, reach.length)
        chord_x = lengths[0]+lengths[1]*math.cos(bend)+lengths[2]*math.cos(2*bend)
        chord_y = lengths[1]*math.sin(bend)+lengths[2]*math.sin(2*bend)
        offset = math.atan2(chord_y, chord_x)
        pole = self.palm-direction*self.palm.dot(direction)
        if pole.length < .001:
            pole = distal-direction*distal.dot(direction)
        pole.normalize()
        first_axis = direction*math.cos(offset)+pole*math.sin(offset)
        bend_axis = direction*math.sin(offset)-pole*math.cos(offset)
        posed = [heads[0].copy()]
        for i, length in enumerate(lengths):
            segment = first_axis*math.cos(i*bend)+bend_axis*math.sin(i*bend)
            posed.append(posed[-1]+segment*length)
        segments = [posed[i+1]-posed[i] for i in range(3)]
        length_error = max(abs(segment.length-length) for segment, length in zip(segments, lengths))
        target_error = (posed[-1]-target).length
        hinge_degrees = [math.degrees(segments[i].angle(segments[i+1])) for i in range(2)]
        if length_error > .000002 or target_error > .000002:
            raise ValueError('Thumb arc changed original segment lengths or authored target')
        if any(not 15 < angle < 65.01 for angle in hinge_degrees):
            raise ValueError('Thumb arc exceeded moderate anatomical bend guard')
        self.thumb_audit = {
            'method': 'continuous three-segment source-bone arc; no target clamp',
            'hingeDegrees': [round(angle, 5) for angle in hinge_degrees],
            'sourceSegmentLengthsMeters': [round(length, 7) for length in lengths],
            'requestedReachMeters': round(reach.length, 7),
            'achievedReachMeters': round((posed[-1]-heads[0]).length, 7),
            'maximumSegmentLengthErrorMeters': round(length_error, 9),
            'targetErrorMeters': round(target_error, 9)}
        for i, name in enumerate(thumb_names):
            self.transforms[name] = _segment_matrix(heads[i], tails[i], posed[i], posed[i+1])

        # Original source IDs, never nearest vertices from another hand/body.
        source_ids = [vi for vi in donor.body_ids
                      if any(n.endswith('.'+side) and n.startswith(('finger', 'metacarpal', 'wrist'))
                             and w > 0 for n, w in self.weights.get(vi, []))]
        self.kd = KDTree(len(source_ids))
        for vi in source_ids:
            self.kd.insert(donor.points[vi], vi)
        self.kd.balance()

    def map_weights(self, position):
        neighbours = self.kd.find_n(position, 3)
        if neighbours[0][2] > .026:
            # The wrist cut can retain lower-forearm vertices. Identity there
            # is correct; never allow a finger corrective to drag a cuff.
            return []
        if neighbours[0][2] < .00002:
            return self.weights[neighbours[0][1]]
        merged = {}
        contributions = [1/max(.0005, distance)**2 for _, _, distance in neighbours]
        total = sum(contributions)
        for (_, vi, _), contribution in zip(neighbours, contributions):
            for name, weight in self.weights[vi]:
                merged[name] = merged.get(name, 0)+weight*contribution/total
        return list(merged.items())

    def deform(self, position, normal):
        weights = self.map_weights(position)
        total = sum(weight for _, weight in weights)
        if total < .00001:
            return position.copy(), normal.copy()
        result, result_normal = Vector(), Vector()
        for name, weight in weights:
            matrix = self.transforms.get(name)
            result += (matrix @ position if matrix else position)*(weight/total)
            result_normal += (matrix.to_3x3() @ normal if matrix else normal)*(weight/total)
        if (result-position).length > .18:
            raise ValueError('Unbounded finger corrective')
        return result, result_normal.normalized()


_authors = {}


def _author(donor, side):
    # Both hands and all three LODs reuse the same deterministic source pose.
    key = (str(donor.INPUT), side)
    if key not in _authors:
        _authors[key] = _GripAuthor(donor, side)
    return _authors[key]


def add_grip_shapes(part, donor):
    """Add Grip_L/R deltas to one original hand part, leaving basis untouched.

    Call for retained hand parts only, once per LOD. Returns the affected side
    and maximal vertex travel for the provenance/QA manifest. An unrelated part
    is rejected rather than acquiring a whole-body accidental corrective.
    """
    count = len(part['positions'])//3
    vertices = [Vector(part['positions'][i*3:i*3+3]) for i in range(count)]
    mean = _average(vertices)
    side = 'L' if mean.x > 0 else 'R'
    if abs(mean.x) < .42 or not .90 < mean.y < 1.16:
        raise ValueError('Grip corrective requested for a non-hand part')
    author = _author(donor, side)
    positions, normals, maximum = [], [], 0.0
    for i, vertex in enumerate(vertices):
        normal = Vector(part['normals'][i*3:i*3+3])
        posed, posed_normal = author.deform(vertex, normal)
        delta = posed-vertex
        maximum = max(maximum, delta.length)
        positions.extend(_round3(delta))
        normals.extend(_round3(posed_normal-normal))
    if maximum < .02:
        raise ValueError('Finger corrective did not close the source hand')
    name = 'Grip_'+side
    existing = [shape for shape in part.get('blendShapes', []) if shape['name'] != name]
    part['blendShapes'] = existing+[{'name': name, 'deltaPositions': positions, 'deltaNormals': normals}]
    return {'side': side, 'maximumDeltaMeters': round(maximum, 6),
            'source': 'original CC0 default.mhskel finger joints + default_weights.mhw',
            'thumbPoseAudit': author.thumb_audit,
            'runtimeRigChanged': False}


def grip_attachment(donor, side='R'):
    """Draft socket-local kilic transform and contact axes, in canonical metres.

    Weapon +Y follows palm width toward thumb/index; +Z is palmward. Its grip
    midpoint is (-.006,-.080,0), not the guard-centred prefab origin. Unity uses
    quaternion [x,y,z,w]. The unchanged hand bone has identity bind rotation.
    """
    author = _author(donor, side)
    right = author.width.cross(author.palm).normalized()
    rotation = Matrix((right, author.width, author.palm)).transposed()
    quaternion = rotation.to_quaternion()
    position = author.center-author.wrist-rotation @ Vector((-.006, -.080, 0))
    return {'side': side, 'localPosition': _round3(position),
            'localRotation': _round3((quaternion.x, quaternion.y, quaternion.z, quaternion.w)),
            'gripCenterLocal': _round3(author.center-author.wrist),
            'gripAxisLocal': _round3(author.width),
            'palmNormalLocal': _round3(author.palm),
            'weaponGripCenter': [-.006, -.080, 0], 'status': 'DRAFT_REQUIRES_RENDERED_QA'}
