// Read-only source probe: mathematical neighbor-switch discontinuity, NOT a
// measured player-frame pop or proof that the actual animation follows these
// straight feature-space chords. Writes only an ignored diagnostic JSON file.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const cp = require('child_process');
const root = path.resolve(__dirname, '../..');
const relativeSource = 'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/HasanDonor/CLTH_Donor_HasanCoatDonor.focmesh.json';
const bytes = fs.readFileSync(path.join(root, relativeSource));
const source = JSON.parse(bytes);
const samples = source.poseCorrectives;
const shapes = Object.fromEntries(source.lods[0].parts[0].blendShapes.map(s => [s.name, s.deltaPositions]));
function nearest(features) {
  return samples.map((sample, index) => ({ index,
    squaredDistance: sample.features.reduce((sum, value, k) => sum + (value-features[k])**2, 0)
  })).sort((a, b) => a.squaredDistance-b.squaredDistance).slice(0, 4);
}
const events = [];
for (const gait of ['Walk', 'Run']) for (let step=0; step<8; step++) {
  const a = samples.find(s => s.name === gait+(step*125));
  const b = samples.find(s => s.name === gait+(((step+1)%8)*125));
  if (!a || !b) continue;
  const features = t => a.features.map((value, k) => value+(b.features[k]-value)*t);
  let previous = nearest(features(0));
  for (let k=1; k<=500; k++) {
    const current = nearest(features(k/500));
    const oldSet = previous.slice(0, 3).map(n => n.index);
    const newSet = current.slice(0, 3).map(n => n.index);
    const removed = oldSet.filter(i => !newSet.includes(i));
    const entered = newSet.filter(i => !oldSet.includes(i));
    if (removed.length === 1 && entered.length === 1) {
      const i = removed[0], j = entered[0];
      const difference = t => {
        const f = features(t);
        return samples[i].features.reduce((sum, value, d) => sum+(value-f[d])**2, 0)
             - samples[j].features.reduce((sum, value, d) => sum+(value-f[d])**2, 0);
      };
      let lo = (k-1)/500, hi = k/500;
      const start = difference(lo);
      for (let iteration=0; iteration<40; iteration++) {
        const middle = (lo+hi)/2;
        if (difference(middle)*start > 0) lo = middle; else hi = middle;
      }
      const t = (lo+hi)/2;
      const n = nearest(features(t));
      // At exact authored features, single-shape weighting makes other weights
      // tend toward zero; those points are not the discontinuity counterexample.
      if (n[0].squaredDistance > 1e-8) {
        const distance = n.find(v => v.index === i)?.squaredDistance ?? n[2].squaredDistance;
        const weight = (1/distance)/n.slice(0,3).reduce((sum, v) => sum+1/v.squaredDistance, 0);
        const left = shapes['Pose_'+samples[i].name], right = shapes['Pose_'+samples[j].name];
        let maxJump = 0, vertex = -1;
        for (let v=0; v<left.length; v+=3) {
          const jump = Math.hypot(left[v]-right[v], left[v+1]-right[v+1], left[v+2]-right[v+2])*weight;
          if (jump > maxJump) { maxJump=jump; vertex=v/3; }
        }
        events.push({ gait, from:a.name, to:b.name, chordParameter:t,
          dropping:samples[i].name, entering:samples[j].name, nonzeroSwitchWeight:weight,
          maxBindJumpMeters:maxJump, maxJumpVertex:vertex });
      }
    }
    previous = current;
  }
}
events.sort((a,b) => b.maxBindJumpMeters-a.maxBindJumpMeters);
const report = {
  status:'MATHEMATICAL_COUNTEREXAMPLE_NOT_PLAYER_MEASUREMENT',
  source:relativeSource,
  sourceSha256:crypto.createHash('sha256').update(bytes).digest('hex'),
  sourceRevision:source.revision,
  headSha:cp.execFileSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8'}).trim(),
  worktree:cp.execFileSync('git',['status','--porcelain'],{cwd:root,encoding:'utf8'}).trim().length ? 'dirty' : 'clean',
  algorithm:'Linear chords between adjacent authored Walk/Run18D feature vectors; detect third/fourth-neighbor swap; bisect40times; jump = tieWeight*(deltaDropped-deltaEntered).',
  scope:'LOD0 existing coat basis; unmodified JSON source. No Unity/Blender, no rendered-frame measurement.',
  limitations:['Actual animation features are not proven to follow these linear chords.',
    'Numbers are bind-space shape jumps, not posed world-space frame displacements.',
    'Demonstrates no global temporal-continuity guarantee for nearest3 truncation.'],
  events
};
const output = path.join(root, 'TestResults/HasanDonor/nearest3-continuity-probe.json');
fs.mkdirSync(path.dirname(output), { recursive:true });
fs.writeFileSync(output, JSON.stringify(report,null,2));
console.log(JSON.stringify({output,sourceSha256:report.sourceSha256,largest:events.slice(0,3)},null,2));
