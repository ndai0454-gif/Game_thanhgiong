"""Clean the ActionForge rigged copy; preserve the downloaded Meshy original."""
import json
import struct
from pathlib import Path
import numpy as np

class Glb:
    def __init__(self, path):
        raw = Path(path).read_bytes()
        length = struct.unpack_from('<I', raw, 12)[0]
        self.doc = json.loads(raw[20:20+length])
        offset = 20+length
        size = struct.unpack_from('<I', raw, offset)[0]
        self.bin = bytearray(raw[offset+8:offset+8+size])

    def array(self, index):
        a = self.doc['accessors'][index]
        v = self.doc['bufferViews'][a['bufferView']]
        d = np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']])
        width = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        start = v.get('byteOffset',0)+a.get('byteOffset',0)
        return np.ndarray((a['count'],width),dtype=d,buffer=self.bin,offset=start,strides=(v.get('byteStride',width*d.itemsize),d.itemsize)).copy()

    def set_array(self, index, data):
        data = np.ascontiguousarray(data)
        self.bin.extend(b'\0'*(-len(self.bin)%4))
        v = len(self.doc['bufferViews'])
        self.doc['bufferViews'].append({'buffer':0,'byteOffset':len(self.bin),'byteLength':data.nbytes})
        self.bin.extend(data.tobytes())
        a = self.doc['accessors'][index]
        a['bufferView'] = v
        a['byteOffset'] = 0
        a['count'] = len(data)
        a.pop('min',None)
        a.pop('max',None)
        if a['type'] != 'MAT4':
            a['min'] = data.min(axis=0).tolist()
            a['max'] = data.max(axis=0).tolist()

    def save(self,path):
        self.bin.extend(b'\0'*(-len(self.bin)%4))
        self.doc['buffers'] = [{'byteLength':len(self.bin)}]
        js = json.dumps(self.doc,separators=(',',':')).encode()
        js += b' ' * (-len(js)%4)
        out = struct.pack('<III',0x46546c67,2,28+len(js)+len(self.bin))
        out += struct.pack('<II',len(js),0x4e4f534a)+js
        out += struct.pack('<II',len(self.bin),0x004e4942)+self.bin
        Path(path).parent.mkdir(parents=True,exist_ok=True)
        Path(path).write_bytes(out)

if __name__ == '__main__':
    import argparse
    p = argparse.ArgumentParser()
    p.add_argument('source')
    p.add_argument('--inspect',action='store_true')
    p.add_argument('--output')
    args=p.parse_args()
    g=Glb(args.source)
    prim=g.doc['meshes'][0]['primitives'][0]
    pos=g.array(prim['attributes']['POSITION'])
    print('bounds',pos.min(axis=0),pos.max(axis=0))
    print('triangles',len(g.array(prim['indices']))//3)
    print('joints',[(j,g.doc['nodes'][n].get('name')) for j,n in enumerate(g.doc['skins'][0]['joints'])])
    if args.output:
        attrs=prim['attributes']
        triangles=g.array(prim['indices']).reshape(-1,3)
        # Meshy made a low rocky plinth fused to the boots. Cut its low plane
        # on this copy, keeping the original download for editable recovery.
        keep=(pos[triangles,1].min(axis=1)>=0.142)
        selected=triangles[keep]
        used,remap=np.unique(selected,return_inverse=True)
        joints=g.array(attrs['JOINTS_0'])
        weights=g.array(attrs['WEIGHTS_0'])
        hand=next(j for j,n in enumerate(g.doc['skins'][0]['joints']) if g.doc['nodes'][n].get('name')=='hand_r')
        # The sword extends outwards from the right clenched hand in this
        # T-pose. It must keep one rigid transform rather than finger blending.
        sword=(pos[:,0]<-0.50)&(pos[:,1]>1.15)&(pos[:,1]<1.60)
        joints[sword]=[hand,0,0,0]
        weights[sword]=[1,0,0,0]
        arrays={name:g.array(index) for name,index in attrs.items()}
        arrays['JOINTS_0']=joints
        arrays['WEIGHTS_0']=weights
        for name,index in attrs.items():
            g.set_array(index,arrays[name][used])
        g.set_array(prim['indices'],remap.astype(np.uint32).reshape(-1,1))
        g.doc['nodes'][next(i for i,n in enumerate(g.doc['nodes']) if 'mesh' in n)]['name']='GiacAnGeneral_Cleaned'
        assert np.isfinite(weights).all() and np.allclose(weights.sum(axis=1),1,atol=0.001)
        g.save(args.output)
        report={'input':args.source,'output':args.output,'original_triangles':len(triangles),'output_triangles':len(selected),
                'removed_plinth_triangles':int((~keep).sum()),'rigid_sword_vertices':int(sword[used].sum()),
                'cut_height_in_rig_units':0.142,'hand_joint_index':hand,'joint_count':len(g.doc['skins'][0]['joints']),
                'weights_normalized':True,'note':'Face-level pedestal trim; original source preserved. Inspect soles, sword and cape in motion.'}
        Path(args.output).with_suffix('.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
        print(json.dumps(report,indent=2))
