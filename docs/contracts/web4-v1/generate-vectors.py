"""Independent authoring tool: expected bytes/hashes use Python JSON + Decimal + hashlib.
Never imports/runs the JS oracle. Run only for an intentional contract revision.
"""
from pathlib import Path
import json, hashlib, copy
from decimal import Decimal, localcontext
P=Path(__file__).resolve().parent
EPOCH='11111111-2222-4333-8444-555555555555'
OP='aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee'
def load(name):return json.loads((P/name).read_text(encoding='utf-8'))
def write(name,value):(P/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def canon(intent):
 x=copy.deepcopy(intent);body=x['body']
 if not x['operationKind'].endswith('.Post'):
  body.setdefault('note',None)
  for line in body['lines']:
   for k in (('qty','unitPrice') if x['operationKind'].startswith('Sales.') else ('quantity','unitCost')):
    with localcontext() as ctx:
     ctx.prec=80;line[k]=format(Decimal(line[k]),'.4f')
 if 'expectedVersion' in body and x['operationKind'].startswith('Sales.'):
  import base64
  body['expectedVersion']=base64.b64encode(base64.b64decode(body['expectedVersion'])).decode('ascii')
 x['contract']='web4-v1'
 return json.dumps(x,ensure_ascii=False,sort_keys=True,separators=(',',':'))
def digest(intent):return hashlib.sha256(canon(intent).encode('utf-8')).hexdigest()
intents={};fixtures=[]
for module,prefix,resource in [('Sales','sales','saleId'),('Receipt','receipt','receiptId')]:
 route='/api/v1/'+('sales-receipts' if module=='Sales' else 'goods-receipts')
 for action in ['Create','Update','Post']:
  stem=f'{prefix}-{action.lower()}';result=load(stem+'-response.json');body=load(stem+'-request.json')
  intent={'operationKind':module+'.'+action,'method':'PUT' if action=='Update' else 'POST','routeScope':route+('' if action=='Create' else '/{'+resource+'}/'+('draft' if action=='Update' else 'post')),'requestTargetId':None if action=='Create' else result['data'][resource],'body':body}
  intents[stem]=intent;write(stem+'-intent.json',intent)
  status={'data':{'deploymentEpoch':EPOCH,'operationId':OP,**{k:v for k,v in intent.items() if k!='body'},'requestFingerprint':digest(intent),'state':'COMMITTED','outcome':result}}
  write(stem+'-status.json',status)
  fixtures.extend([{'file':stem+'-request.json','kind':'body','operationKind':module+'.'+action,'intent':stem+'-intent.json'}, {'file':stem+'-intent.json','kind':'intent'}, {'file':stem+'-response.json','kind':'outcome','operationKind':module+'.'+action,'status':200},{'file':stem+'-status.json','kind':'status','intent':stem+'-intent.json','status':200}])
vectors=[]
def vector(name,intent):
 vectors.append({'name':name,'intent':copy.deepcopy(intent),'canonical':canon(intent),'sha256':digest(intent)})
for stem,intent in intents.items():vector(stem,intent)
x=copy.deepcopy(intents['sales-create']);x['body']['lines']=[x['body']['lines'][0]];vector('one-line',x)
x=copy.deepcopy(intents['sales-create']);x['body']['lines']=[{**x['body']['lines'][i%3],'productId':i+1} for i in range(200)];vector('200-ordered-sales-lines',x)
x=copy.deepcopy(intents['receipt-create']);x['body']['lines']=[{**x['body']['lines'][i%2],'productId':i+1} for i in range(200)];vector('200-ordered-receipt-lines',x)
x=copy.deepcopy(intents['sales-create']);x['body']['lines'][0]['qty']='99999999999999.9999';x['body']['lines'][0]['unitPrice']='999999999999999.9999';vector('max-exact-decimal',x)
x=copy.deepcopy(intents['sales-create']);x['body']['lines'][0]['qty']='0001.25';vector('equivalent-precision',x)
x=copy.deepcopy(intents['sales-create']);del x['body']['note'];vector('missing-note',x)
x['body']['note']=None;vector('null-note',x)
x['body']['note']='';vector('empty-note',x)
x['body']['note']='Tiếng Việt 😀 / \\ "\n\t\b\f\r\u0001\u2028\u2029';vector('unicode-escaping',x)
x['body']['note']='é';vector('unicode-composed',x)
x['body']['note']='e\u0301';vector('unicode-decomposed-distinct',x)
x=copy.deepcopy(intents['sales-create']);x['body']['lines'].reverse();vector('line-order-different',x)
x=copy.deepcopy(intents['sales-create']);x['body']['lines'][0]['isGift']=True;vector('gift-different',x)
x=copy.deepcopy(intents['sales-update']);x['body']['expectedVersion']='AAAAAAAAAAM=';vector('version-different',x)
x['body']['expectedVersion']=' AA\tAAAAAAAAM=\r\n';vector('sales-version-whitespace-equivalent',x)
x=copy.deepcopy(intents['sales-update']);x['body']['expectedVersion']='AAAAAAAAAAF=';vector('sales-version-padbits-equivalent',x)
x=copy.deepcopy(intents['sales-post']);x['requestTargetId']='9007199254740993';vector('target-different',x)
x=copy.deepcopy(intents['sales-update']);x['body']['lines'][0]['saleLineId']='9007199254740995';vector('line-id-different',x)
write('fingerprint-vectors.json',{'algorithm':'SHA-256','independentAuthor':'Python json/decimal/hashlib; no JS oracle import','vectors':vectors})
write('context-response.json',{'data':{'deploymentEpoch':EPOCH,'actorUserId':42,'contractVersion':'web4-v1'}});fixtures.append({'file':'context-response.json','kind':'context','status':200})
for state in ['RESULT_EXPIRED','RESULT_UNAVAILABLE']:
 status=load('sales-create-status.json');status['data']['state']=state;status['data']['outcome']=None
 name='status-'+state.lower().replace('_','-')+'.json';write(name,status);fixtures.append({'file':name,'kind':'status','intent':'sales-create-intent.json','status':200})
errors=[(400,'ValidationError'),(401,'Auth.Unauthorized'),(403,'Auth.Forbidden'),(409,'Deployment.EpochMismatch'),(409,'Idempotency.FingerprintMismatch'),(409,'Idempotency.KeyConflict'),(404,'Operation.NotObserved'),(410,'Operation.ResultExpired'),(503,'Operation.ResultUnavailable'),(503,'Operation.Unavailable'),(500,'InternalError')]
for status,code in errors:
 name='error-'+code.lower().replace('.','-')+'.json';write(name,{'error':{'code':code,'message':'The operation cannot be resolved.' if code!='ValidationError' else 'The request is invalid.','details':[],'correlationId':'web4-corpus-001'}})
 fixtures.append({'file':name,'kind':'error','status':status})
write('protocol-scenarios.json',{'scenarios':[
 {'name':'persist-before-dispatch','status':'Operation.NotObserved','httpStatus':404,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'USER_CONFIRMED_SAME_INTENT_AFTER_B03_B04_PROOF'},
 {'name':'delayed-first-attempt-races-confirmed-resend','status':'COMMITTED','httpStatus':200,'affirmative':True,'automaticResend':False,'newKeyAllowed':False,'resolution':'ONE_SQL_SERIALIZED_COMMIT_REQUIRED_NOT_PROVEN_B01'},
 {'name':'write-without-read','status':'COMMITTED','httpStatus':200,'affirmative':True,'automaticResend':False,'newKeyAllowed':False,'resolution':'CURRENT_ORIGINAL_WRITE_PERMISSION_FULL_ORIGINAL_RESULT_NO_GET'},
 {'name':'post-without-read','status':'COMMITTED','httpStatus':200,'affirmative':True,'automaticResend':False,'newKeyAllowed':False,'resolution':'CURRENT_ORIGINAL_POST_PERMISSION_ORIGINAL_ACK_NO_GET'},
 {'name':'expired-tombstone','status':'RESULT_EXPIRED','httpStatus':200,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'OPERATOR_RECONCILIATION_EDIT_LOCKED'},
 {'name':'revoked-current-grant','status':'Auth.Forbidden','httpStatus':403,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'NO_OUTCOME_DISCLOSED_EDIT_LOCKED'},
 {'name':'cross-actor-equals-missing','status':'Operation.NotObserved','httpStatus':404,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'NO_EXISTENCE_LEAK_EDIT_LOCKED'},
 {'name':'same-key-changed-body-route-version','status':'Idempotency.KeyConflict','httpStatus':409,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'ORIGINAL_UNKNOWN_INTENT_REMAINS_LOCKED'},
 {'name':'changed-db-same-origin-actor','status':'Deployment.EpochMismatch','httpStatus':409,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'NO_DISPATCH_NO_DISCLOSURE'},
 {'name':'client-fingerprint-corrupted','status':'Idempotency.FingerprintMismatch','httpStatus':409,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'RECOMPUTE_FROM_FROZEN_INPUT_NEVER_TRUST_CACHED_HASH'},
 {'name':'late-response-prior-session','status':'COMMITTED','httpStatus':200,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'CALLER_ACTOR_GENERATION_EPOCH_ADMISSION_REJECTS'},
 {'name':'timeout-or-abort','status':'UNKNOWN','httpStatus':None,'affirmative':False,'automaticResend':False,'newKeyAllowed':False,'resolution':'CLIENT_ABORT_IS_NOT_ROLLBACK_PROOF'}]})
write('fixture-index.json',fixtures)
