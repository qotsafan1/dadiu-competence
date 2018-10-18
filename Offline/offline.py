import numpy as np
import math
import sys
import json
import os.path
import pickle

class Object:
    def toJSON(self):
        return json.dumps(self, default=lambda o: o.__dict__,
            sort_keys=True, indent=4)

def line_to_floatlist(l):
    '''
    e.g. turns '  OFFSET  -8.881  -0.000  0.019' into [-8.881, -0.000, 0.019]
    '''
    lst = list(filter(None, l.split('\t')))
    return [float(x) for x in lst[1:]]

def get_end_offset(f):
    f.readline() # {
    offset = line_to_floatlist(f.readline())
    f.readline() # }
    return offset

def read_block(f):
    '''
    f : file
    po : previous offset to calculate the bone length
    '''
    block = [{'angles': []}]

    f.readline() # {

    # Length
    # We use the offset of current bone to find the length of the previous bone
    prev_len = np.linalg.norm(line_to_floatlist(f.readline()))
    f.readline() # Channels
    l = list(filter(None, f.readline().split('\t'))) # Either 'End Site' or 'JOINT'
    end_offset = None
    next_offset = None
    length = None
    if l[0] == "End Site\n":
        end_offset = get_end_offset(f)
        length = np.linalg.norm(end_offset)
        l = list(filter(None, f.readline().split('\t')))
    while l[0] != '}\n':
        next_len, next_block = read_block(f)
        length = length if length and length > next_len else next_len
        block += next_block
        l = list(filter(None, f.readline().split('\t')))
    block[0]['length'] = length
    return (prev_len + length, block)

def str_is_f(x):
    # Checks if a string can be parsed as a float
    try:
        float(x)
        return True
    except ValueError:
        return False

def parse_bvh(file):
    f = open(file, 'r')
    #data = {'length': 0.5, 'bones': [], 'angles': [[], [], []]}
    data = [{'length': 1000, 'angles': []}]
    f.readline() #HIERARCHY
    f.readline() #ROOT  RokokoGuy_Hips
    f.readline() #{
    f.readline() #OFFSET    0.000   0.000   0.000
    f.readline() #CHANNELS
    f.readline() #JOINT
    data += read_block(f)[1] # Left leg
    f.readline() #JOINT
    data += read_block(f)[1] # Right leg
    f.readline() #JOINT
    data += read_block(f)[1] # Spine
    f.readline()
    f.readline()
    f.readline()
    f.readline()
    for line in f:
        lst = list(filter(None, line.split('\t')))
        flst = [float(x) for x in lst if str_is_f(x)]
        data[0]['angles'].append(np.array(flst[:6]).tolist())
        for i in range(1, len(data)):
            bone = data[i]
            bone['angles'].append(np.array(flst[(i-1)*3+6:(i-1)*3+9]).tolist())
    f.close()
    return data

def normalize_angle(angle):
    while angle > 180:
        angle -= 360
    while angle < -180:
        angle += 360
    return angle


def similarity(anim_a, frame_a, anim_b, frame_b):
    # Normalize angles
    na = np.vectorize(normalize_angle)

    comp_frame_a = frame_a + 1
    if comp_frame_a == len(anim_a[0]['angles']):
        comp_frame_a -= 1
        frame_a -= 1
    comp_frame_b = frame_b + 1
    if comp_frame_b == len(anim_b[0]['angles']):
        comp_frame_b -= 1
        frame_b -= 1

    anim_a_root_angles = anim_a[0]['angles']
    anim_b_root_angles = anim_b[0]['angles']
    vroot_a = anim_a_root_angles[comp_frame_a][:3] - anim_a_root_angles[frame_a][:3]
    vroot_b = anim_b_root_angles[comp_frame_b][:3] - anim_b_root_angles[frame_b][:3]
    vroot = anim_a[0]['length'] * np.linalg.norm(vroot_b - vroot_a)**2

    angle_root_vel_a = na(na(anim_a_root_angles[comp_frame_a][3:]) - na(anim_a_root_angles[frame_a][3:]))
    angle_root_vel_b = na(na(anim_b_root_angles[comp_frame_b][3:]) - na(anim_b_root_angles[frame_b][3:]))
    angle_root_vel = anim_a[0]['length'] * np.linalg.norm(angle_root_vel_b - angle_root_vel_a)**2

    angles = 0
    angle_velocities = 0
    for i in range(1, len(anim_a)):
        anim_a_angles = anim_a[i]['angles']
        anim_b_angles = anim_b[i]['angles']
        diff = na(na(anim_a_angles[comp_frame_a]) - na(anim_b_angles[comp_frame_b]))
        angles += anim_a[i]['length'] * np.linalg.norm(diff)**2

        v_a = na(na(anim_a_angles[comp_frame_a]) - na(anim_a_angles[frame_a]))
        v_b = na(na(anim_b_angles[comp_frame_b]) - na(anim_b_angles[frame_b]))
        angle_velocities += anim_a[i]['length'] * np.linalg.norm(v_b - v_a)**2
    angles /= 100

    return math.sqrt(vroot+angle_root_vel+angles+angle_velocities)

def create_sim_file():
    animations = [('readyShort', 'Shared/ready_short_', 'pullback', 'Fight/windup_04.bvh', [0.173, 0.26]),
                  ('pullback', 'Fight/windup_04.bvh', 'punchGroin0', 'Fight/punch_crotch_01.bvh', [0.027, 0.031]),
                  ('pullback', 'Fight/windup_04.bvh', 'punchHead0', 'Fight/punch_head_01.bvh', [0.28, 0.39]),
                  ('pullback', 'Fight/windup_04.bvh', 'punchTorso0', 'Fight/punch_chest_01.bvh', [0.285, 0.416]),
                  ('pullback', 'Fight/windup_04.bvh', 'miss', 'Fight/miss_01.bvh', [0.031, 0.045]),
                  ('punchGroin0', 'Fight/punch_crotch_01.bvh', 'readyShort', 'Shared/ready_short_', 0.8),
                  ('punchHead0', 'Fight/punch_head_01.bvh', 'readyShort', 'Shared/ready_short_', 0.8),
                  ('punchTorso0', 'Fight/punch_chest_01.bvh', 'readyShort', 'Shared/ready_short_', 0.8),
                  ('miss', 'Fight/miss_01.bvh', 'readyShort', 'Shared/ready_short_', 0.8),
                  ('readyShort', 'Shared/ready_short_', 'hitGroin0', 'Fight/hit_groin_0_', [0.165, 0.25]),
                  ('hitGroin0', 'Fight/hit_groin_0_', 'hitGroin0', 'Fight/hit_groin_0_', [0.165, 0.25]),
                  ('hitHead0', 'Fight/hit_head_0_', 'hitGroin0', 'Fight/hit_groin_0_', [0.165, 0.25]),
                  ('hitTorso0', 'Fight/hit_torso_0_', 'hitGroin0', 'Fight/hit_groin_0_', [0.165, 0.25]),
                  ('punchGroin0', 'Fight/punch_crotch_01.bvh', 'hitGroin0', 'Fight/hit_groin_0_', [0.165, 0.25]),
                  ('punchHead0', 'Fight/punch_head_01.bvh', 'hitGroin0', 'Fight/hit_groin_0_', [0.165, 0.25]),
                  ('punchTorso0', 'Fight/punch_chest_01.bvh', 'hitGroin0', 'Fight/hit_groin_0_', [0.165, 0.25]),
                  ('readyShort', 'Shared/ready_short_', 'hitHead0', 'Fight/hit_head_0_', [0.369, 0.39]),
                  ('hitHead0', 'Fight/hit_head_0_', 'hitHead0', 'Fight/hit_head_0_', [0.369, 0.39]),
                  ('hitGroin0', 'Fight/hit_groin_0_', 'hitHead0', 'Fight/hit_head_0_', [0.369, 0.39]),
                  ('hitTorso0', 'Fight/hit_torso_0_', 'hitHead0', 'Fight/hit_head_0_', [0.369, 0.39]),
                  ('punchGroin0', 'Fight/punch_crotch_01.bvh', 'hitHead0', 'Fight/hit_head_0_', [0.369, 0.39]),
                  ('punchHead0', 'Fight/punch_head_01.bvh', 'hitHead0', 'Fight/hit_head_0_', [0.369, 0.39]),
                  ('punchTorso0', 'Fight/punch_chest_01.bvh', 'hitHead0', 'Fight/hit_head_0_', [0.369, 0.39]),
                  ('readyShort', 'Shared/ready_short_', 'hitTorso0', 'Fight/hit_torso_0_', [0.23, 0.299]),
                  ('hitTorso0', 'Fight/hit_torso_0_', 'hitTorso0', 'Fight/hit_torso_0_', [0.23, 0.299]),
                  ('hitHead0', 'Fight/hit_head_0_', 'hitTorso0', 'Fight/hit_torso_0_', [0.23, 0.299]),
                  ('hitGroin0', 'Fight/hit_groin_0_', 'hitTorso0', 'Fight/hit_torso_0_', [0.23, 0.299]),
                  ('punchGroin0', 'Fight/punch_crotch_01.bvh', 'hitTorso0', 'Fight/hit_torso_0_', [0.23, 0.299]),
                  ('punchHead0', 'Fight/punch_head_01.bvh', 'hitTorso0', 'Fight/hit_torso_0_', [0.23, 0.299]),
                  ('punchTorso0', 'Fight/punch_chest_01.bvh', 'hitTorso0', 'Fight/hit_torso_0_', [0.23, 0.299]),
                  ('hitGroin0', 'Fight/hit_groin_0_', 'readyShort', 'Shared/ready_short_', 0.8),
                  ('hitHead0', 'Fight/hit_head_0_', 'readyShort', 'Shared/ready_short_', 0.8),
                  ('hitTorso0', 'Fight/hit_torso_0_', 'readyShort', 'Shared/ready_short_', 0.8)]
    transitions = {}
    frames = {}

    loaded_transitions = {}
    if os.path.isfile("transitions.pkl"):
        with open("transitions.pkl", "rb") as f:
            loaded_transitions = pickle.load(f)

    num_transitions = len(animations)
    current_trans = 1

    for anim_a_name, anim_a_file, anim_b_name, anim_b_file, max_time in animations:
        print(anim_a_name + " " + anim_b_name)
        anim_a = parse_bvh(anim_a_file)
        anim_b = parse_bvh(anim_b_file)

        if anim_a_name not in frames:
            frames[anim_a_name] = len(anim_a[0]['angles'])
        if anim_b_name not in frames:
            frames[anim_b_name] = len(anim_b[0]['angles'])

        # If we have previously calculated the transitions, we just load it
        if anim_a_name in loaded_transitions and anim_b_name in loaded_transitions[anim_a_name]:
            current_trans += 1
            if anim_a_name not in transitions:
                transitions[anim_a_name] = {}
            transitions[anim_a_name][anim_b_name] = loaded_transitions[anim_a_name][anim_b_name]
            continue

        if anim_a_name not in transitions:
            transitions[anim_a_name] = {}

        transitions[anim_a_name][anim_b_name] = []
        num_frames = len(anim_a[0]['angles'])
        for i in range(num_frames):
            best_score = -1
            best_frame = 0
            if type(max_time) is list:
                rang = range(int(len(anim_b[0]['angles'])*max_time[0]),int(len(anim_b[0]['angles'])*max_time[1]))
            else:
                rang = range(int(len(anim_b[0]['angles'])*max_time))
            for j in rang:
                score = similarity(anim_a, i, anim_b, j)
                if score < best_score or best_score == -1:
                    best_score = score
                    best_frame = j
            transitions[anim_a_name][anim_b_name].append(best_frame)
            sys.stdout.write("Progress: %d%% (%d/%d)   \r" % (i/num_frames*100, current_trans, num_transitions) )
            sys.stdout.flush()
        current_trans += 1

    with open("transitions.json", "w") as f:
        f.write(json.dumps(transitions))

    with open("transitions.pkl", "wb") as f:
        pickle.dump(transitions, f)

    with open("frames.json", "w") as f:
        f.write(json.dumps(frames))


if __name__ == '__main__':    
    with open("large-w-circle.json", "w") as f:
        f.write(json.dumps(parse_bvh("large-w-circle.bvh")))