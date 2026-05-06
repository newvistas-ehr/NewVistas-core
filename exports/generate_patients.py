#!/usr/bin/env python3
"""Generate synthetic VistA ZWR test data with configurable patient counts.

Usage:
    python generate_patients.py [50|500|1000]

Default is 500 patients. Output goes to Fifty/, FiveHundred/, or OneThousand/.
"""

import random
import os
import sys

random.seed(42)  # Reproducible output

# ── Parse command-line argument ────────────────────────────────────────────

VALID_COUNTS = {50: "Fifty", 500: "FiveHundred", 1000: "OneThousand"}

if len(sys.argv) > 1:
    try:
        patient_count = int(sys.argv[1])
    except ValueError:
        print(f"Error: '{sys.argv[1]}' is not a valid patient count. Use 50, 500, or 1000.")
        sys.exit(1)
    if patient_count not in VALID_COUNTS:
        print(f"Error: Patient count must be 50, 500, or 1000. Got {patient_count}.")
        sys.exit(1)
else:
    patient_count = 500

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), VALID_COUNTS[patient_count])
os.makedirs(OUT_DIR, exist_ok=True)

print(f"Generating data for {patient_count} patients -> {OUT_DIR}")

# ── Name pools ─────────────────────────────────────────────────────────────

LAST_NAMES = [
    "SMITH","JOHNSON","WILLIAMS","BROWN","JONES","GARCIA","MILLER","DAVIS",
    "RODRIGUEZ","MARTINEZ","HERNANDEZ","LOPEZ","GONZALEZ","WILSON","ANDERSON",
    "THOMAS","TAYLOR","MOORE","JACKSON","MARTIN","LEE","PEREZ","THOMPSON",
    "WHITE","HARRIS","SANCHEZ","CLARK","RAMIREZ","LEWIS","ROBINSON","WALKER",
    "YOUNG","ALLEN","KING","WRIGHT","SCOTT","TORRES","NGUYEN","HILL","FLORES",
    "GREEN","ADAMS","NELSON","BAKER","HALL","RIVERA","CAMPBELL","MITCHELL",
    "CARTER","ROBERTS","GOMEZ","PHILLIPS","EVANS","TURNER","DIAZ","PARKER",
    "CRUZ","EDWARDS","COLLINS","REYES","STEWART","MORRIS","MORALES","MURPHY",
    "COOK","ROGERS","GUTIERREZ","ORTIZ","MORGAN","COOPER","PETERSON","BAILEY",
    "REED","KELLY","HOWARD","RAMOS","KIM","COX","WARD","RICHARDSON","WATSON",
    "BROOKS","CHAVEZ","WOOD","JAMES","BENNETT","GRAY","MENDOZA","RUIZ",
    "HUGHES","PRICE","ALVAREZ","CASTILLO","SANDERS","PATEL","MYERS","LONG",
    "ROSS","FOSTER","JIMENEZ","POWELL","JENKINS","PERRY","RUSSELL","SULLIVAN",
    "BELL","COLEMAN","BUTLER","HENDERSON","BARNES","GONZALES","FISHER",
    "VASQUEZ","SIMMONS","GRAHAM","MCDONALD","SPENCER","WAGNER","HUNT",
    "FRANKLIN","CUNNINGHAM","LYNCH","BISHOP","RILEY","BARRETT","HART",
    "MASON","FORD","WASHINGTON","HOFFMAN","SNYDER","MALDONADO","DUNN",
    "FREEMAN","BURNS","DRAKE","COLE","WEST","JORDAN","OWENS","REYNOLDS",
    "WARREN","DIXON","HARRISON","GIBSON","KNIGHT","DUNCAN","CARR","HAWKINS",
    "STONE","BLACK","ARMSTRONG","PIERCE","ARNOLD","RAY","WEAVER","OLSON",
    "WEBB","FIELDS","SCHWARTZ","GRANT","TODD","SILVA","MANN","CHEN","VARGAS",
    "AGUILAR","MEDINA","SANTOS","DELEON","PENA","SERRANO","VEGA","ESTRADA",
    "ROJAS","GUERRERO","ACOSTA","SOTO","BAUTISTA","PADILLA","DOMINGUEZ",
    "CONTRERAS","CORTEZ","MONTOYA","OCHOA","FUENTES","SALAZAR","CARDENAS",
    "TRUJILLO","HERRERA","ESPINOZA","GALLEGOS","SANDOVAL","MONTES","MEJIA",
    "VELASQUEZ","NUNEZ","RIOS","MERCADO","OROZCO","CORONA","VILLANUEVA",
]

MALE_FIRST = [
    "JOHN","ROBERT","MICHAEL","DAVID","WILLIAM","RICHARD","CHARLES","THOMAS",
    "CHRISTOPHER","DANIEL","MATTHEW","ANTHONY","MARK","DONALD","STEVEN",
    "ANDREW","KENNETH","JOSHUA","GEORGE","EDWARD","BRIAN","RONALD","TIMOTHY",
    "JASON","JEFFREY","RYAN","JACOB","NICHOLAS","GARY","ERIC","STEPHEN",
    "JONATHAN","LARRY","KEVIN","FRANK","SCOTT","RAYMOND","GREGORY","SAMUEL",
    "BENJAMIN","PATRICK","JACK","DENNIS","JERRY","ALEXANDER","HENRY","CARL",
    "ARTHUR","PETER","ALBERT","LAWRENCE","JESSE","WAYNE","ROGER","BRUCE",
    "RALPH","ROY","EUGENE","RUSSELL","LOUIS","PHILIP","HARRY","VINCENT",
    "BOBBY","DYLAN","RANDY","HOWARD","EUGENE","TRAVIS","DUSTIN","SERGIO",
    "CARLOS","MIGUEL","JOSE","LUIS","JUAN","RICARDO","MARCOS","RAFAEL",
]

FEMALE_FIRST = [
    "MARY","PATRICIA","JENNIFER","LINDA","BARBARA","ELIZABETH","SUSAN",
    "JESSICA","SARAH","KAREN","NANCY","LISA","BETTY","MARGARET","SANDRA",
    "DOROTHY","DONNA","CAROL","RUTH","SHARON","HELEN","DEBORAH","DEBRA",
    "LAURA","DIANE","ALICE","KATHLEEN","VIRGINIA","FRANCES","TERESA",
    "ANN","JEAN","ABIGAIL","JOYCE","MARIE","DENISE","CATHERINE","CHRISTINE",
    "BEVERLY","THERESA","JANET","JULIA","GRACE","AMBER","TIFFANY","NICOLE",
    "BRENDA","TAMMY","RACHEL","REBECCA","VICTORIA","GLORIA","ROSA","MARIA",
    "CARMEN","ELENA","SILVIA","GABRIELA","ADRIANA","FERNANDA","VALERIA",
    "CONNIE","LILLIAN","ROBIN","EMILY","ASHLEY","MEGAN","MICHELLE","KIMBERLY",
    "ANGELA","AMY","STEPHANIE","HEATHER","CHRISTINA","ANNA","KELLY","MARTHA",
]

STREETS = [
    "OAK","MAPLE","ELM","PINE","BIRCH","CEDAR","WALNUT","SPRUCE","HICKORY",
    "MAGNOLIA","POPLAR","SYCAMORE","CHESTNUT","BEECH","REDWOOD","CYPRESS",
    "PALM","CHERRY","DOGWOOD","ASPEN","JUNIPER","LOCUST","ALDER","HAWTHORN",
    "COTTONWOOD","MULBERRY","BASSWOOD","CATALPA","WILLOW","LINDEN","HEMLOCK",
    "PECAN","IRONWOOD","BOXWOOD","LAUREL","MYRTLE","OLIVE","HOLLY","IVY",
    "LILAC","MARIGOLD","PRIMROSE","VERBENA","ASTER","ZINNIA","GARDENIA",
    "PETUNIA","DAHLIA","ORCHID","IRIS","ACACIA","SEQUOIA","MESQUITE","SAGE",
    "ROSEWOOD","BAMBOO","JASMINE","AZALEA","CAMELLIA","FERN","HEATHER",
    "CLOVER","THISTLE","WISTERIA","LARKSPUR","COLUMBINE","FOXGLOVE","VIOLET",
]

STREET_TYPES = ["STREET","AVENUE","DRIVE","ROAD","LANE","COURT","WAY","BOULEVARD","PLACE","CIRCLE"]

CITIES_STATES_ZIPS = [
    ("RICHMOND","VA","23220"),("DALLAS","TX","75201"),("SEATTLE","WA","98101"),
    ("CHICAGO","IL","60601"),("PHOENIX","AZ","85001"),("SAN ANTONIO","TX","78201"),
    ("DENVER","CO","80201"),("PORTLAND","OR","97201"),("MIAMI","FL","33101"),
    ("ATLANTA","GA","30301"),("MINNEAPOLIS","MN","55401"),("BOSTON","MA","02101"),
    ("TAMPA","FL","33601"),("NASHVILLE","TN","37201"),("SALT LAKE CITY","UT","84101"),
    ("COLUMBUS","OH","43201"),("CHARLOTTE","NC","28201"),("JACKSONVILLE","FL","32201"),
    ("SAN FRANCISCO","CA","94101"),("INDIANAPOLIS","IN","46201"),("AUSTIN","TX","78701"),
    ("MILWAUKEE","WI","53201"),("PITTSBURGH","PA","15201"),("ALBUQUERQUE","NM","87101"),
    ("TUCSON","AZ","85701"),("FRESNO","CA","93701"),("SACRAMENTO","CA","95801"),
    ("OMAHA","NE","68101"),("RALEIGH","NC","27601"),("LOUISVILLE","KY","40201"),
    ("VIRGINIA BEACH","VA","23451"),("OKLAHOMA CITY","OK","73101"),("HARTFORD","CT","06101"),
    ("KANSAS CITY","MO","64101"),("MEMPHIS","TN","38101"),("BUFFALO","NY","14201"),
    ("ST LOUIS","MO","63101"),("NEW ORLEANS","LA","70101"),("CLEVELAND","OH","44101"),
    ("LAS VEGAS","NV","89101"),("BALTIMORE","MD","21201"),("DETROIT","MI","48201"),
    ("EL PASO","TX","79901"),("HONOLULU","HI","96801"),("ANCHORAGE","AK","99501"),
    ("SAN DIEGO","CA","92101"),("BOISE","ID","83701"),("COLUMBIA","SC","29201"),
    ("LITTLE ROCK","AR","72201"),("BIRMINGHAM","AL","35201"),("TULSA","OK","74101"),
    ("WICHITA","KS","67201"),("SPOKANE","WA","99201"),("TACOMA","WA","98401"),
    ("HUNTSVILLE","AL","35801"),("FAYETTEVILLE","NC","28301"),("KNOXVILLE","TN","37901"),
    ("MOBILE","AL","36601"),("SAVANNAH","GA","31401"),("NORFOLK","VA","23501"),
    ("DES MOINES","IA","50301"),("DAYTON","OH","45401"),("SIOUX FALLS","SD","57101"),
    ("CHARLESTON","SC","29401"),("SHREVEPORT","LA","71101"),("LUBBOCK","TX","79401"),
    ("LAREDO","TX","78040"),("AMARILLO","TX","79101"),("CORPUS CHRISTI","TX","78401"),
    ("RENO","NV","89501"),("EUGENE","OR","97401"),("FORT WAYNE","IN","46801"),
    ("MADISON","WI","53701"),("GREENVILLE","SC","29601"),("LEXINGTON","KY","40501"),
]

AREA_CODES = [
    "804","214","206","312","602","210","303","503","305","404","612","617",
    "813","615","801","614","704","904","415","317","512","414","412","505",
    "520","559","916","402","919","502","757","405","860","816","901","716",
    "314","504","216","702","410","313","915","808","907","619","208","803",
    "501","205","918","316","509","253","256","910","865","251","912","757",
    "515","937","605","843","318","806","956","806","361","775","541","260",
    "608","864","859",
]

BRANCHES = ["ARMY","NAVY","AIR FORCE","MARINES","COAST GUARD"]
EMERG_RELS = ["SPOUSE","SON","DAUGHTER","BROTHER","SISTER","MOTHER","FATHER","FRIEND","GRANDSON","GRANDDAUGHTER"]

# ── Clinical data pools ────────────────────────────────────────────────────

ALLERGY_DRUGS = [
    ("PENICILLIN","Drug","3;PSDRUG("),("SULFA DRUGS","Drug","15;PSDRUG("),
    ("ASPIRIN","Drug","8;PSDRUG("),("CODEINE","Drug","22;PSDRUG("),
    ("MORPHINE","Drug","30;PSDRUG("),("ERYTHROMYCIN","Drug","12;PSDRUG("),
    ("TETRACYCLINE","Drug","18;PSDRUG("),("METFORMIN","Drug","45;PSDRUG("),
    ("AMOXICILLIN","Drug","5;PSDRUG("),("TRAMADOL","Drug","50;PSDRUG("),
    ("CIPROFLOXACIN","Drug","28;PSDRUG("),("HYDROCODONE","Drug","35;PSDRUG("),
    ("LISINOPRIL","Drug","40;PSDRUG("),("VANCOMYCIN","Drug","55;PSDRUG("),
    ("GABAPENTIN","Drug","60;PSDRUG("),("METOPROLOL","Drug","42;PSDRUG("),
    ("NAPROXEN","Drug","9;PSDRUG("),("OXYCODONE","Drug","33;PSDRUG("),
    ("CLINDAMYCIN","Drug","25;PSDRUG("),("IBUPROFEN","Drug","10;PSDRUG("),
    ("METHOTREXATE","Drug","48;PSDRUG("),("WARFARIN","Drug","65;PSDRUG("),
    ("LIDOCAINE","Drug","38;PSDRUG("),
]
ALLERGY_CLASSES = [
    ("NSAIDS","Drug Class","NSAIDS"),("ACE INHIBITORS","Drug Class","ACE INHIBITORS"),
    ("STATINS","Drug Class","STATINS"),("CEPHALOSPORINS","Drug Class","CEPHALOSPORINS"),
    ("BENZODIAZEPINES","Drug Class","BENZODIAZEPINES"),("FLUOROQUINOLONES","Drug Class","FLUOROQUINOLONES"),
]
ALLERGY_OTHER = [
    ("LATEX","Other","LATEX"),("DUST","Other","DUST"),("BEE STINGS","Other","BEE VENOM"),
    ("CONTRAST DYE","Other","CONTRAST MEDIA"),("ADHESIVE TAPE","Other","ADHESIVE"),
    ("TREE POLLEN","Other","TREE POLLEN"),("CATS","Other","CAT DANDER"),
    ("MOLD","Other","MOLD SPORES"),("PEANUTS","Food","PEANUTS"),
    ("SHELLFISH","Food","SHELLFISH"),("EGGS","Food","EGGS"),("MILK","Food","MILK PRODUCTS"),
]
ALLERGY_REACTIONS = [
    "HIVES","RASH","NAUSEA","VOMITING","SHORTNESS OF BREATH","ITCHING",
    "ANAPHYLAXIS","GI UPSET","DIARRHEA","SWELLING","THROAT SWELLING",
    "RESPIRATORY DISTRESS","DIZZINESS","DROWSINESS","ANGIOEDEMA","COUGH",
    "STOMACH PAIN","SKIN IRRITATION","PHOTOSENSITIVITY","BRADYCARDIA","FATIGUE",
    "GI BLEEDING","MOUTH SORES","SEIZURE","PALPITATIONS","BRUISING",
    "CONGESTION","SNEEZING","WATERY EYES","CONTACT DERMATITIS",
    "RED MAN SYNDROME","C. DIFF COLITIS","TENDON PAIN","TENDON RUPTURE",
]
SEVERITIES = ["MILD","MODERATE","SEVERE"]

# Clinical profiles: (condition_name, icd10, is_chronic, sc_probability)
PROBLEM_POOL = [
    ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
    ("TYPE 2 DIABETES MELLITUS","E11.9","CHRONIC",0.1),
    ("POST-TRAUMATIC STRESS DISORDER","F43.10","CHRONIC",0.8),
    ("MAJOR DEPRESSIVE DISORDER","F33.0","CHRONIC",0.3),
    ("GENERALIZED ANXIETY DISORDER","F41.1","CHRONIC",0.2),
    ("CHRONIC LOW BACK PAIN","M54.5","CHRONIC",0.5),
    ("LUMBAR DEGENERATIVE DISC DISEASE","M51.36","CHRONIC",0.4),
    ("CHRONIC KNEE PAIN","M25.569","CHRONIC",0.5),
    ("TINNITUS","H93.19","CHRONIC",0.8),
    ("BILATERAL HEARING LOSS","H91.93","CHRONIC",0.7),
    ("OBSTRUCTIVE SLEEP APNEA","G47.33","CHRONIC",0.1),
    ("HYPOTHYROIDISM","E03.9","CHRONIC",0.05),
    ("HYPERLIPIDEMIA","E78.5","CHRONIC",0.1),
    ("GASTROESOPHAGEAL REFLUX DISEASE","K21.0","CHRONIC",0.05),
    ("CORONARY ARTERY DISEASE","I25.10","CHRONIC",0.2),
    ("CONGESTIVE HEART FAILURE","I50.9","CHRONIC",0.1),
    ("ATRIAL FIBRILLATION","I48.91","CHRONIC",0.05),
    ("CHRONIC KIDNEY DISEASE STAGE 3","N18.3","CHRONIC",0.05),
    ("ASTHMA","J45.20","CHRONIC",0.1),
    ("COPD","J44.1","CHRONIC",0.3),
    ("OSTEOARTHRITIS OF BOTH KNEES","M17.0","CHRONIC",0.1),
    ("RHEUMATOID ARTHRITIS","M06.9","CHRONIC",0.05),
    ("CHRONIC MIGRAINE","G43.709","CHRONIC",0.3),
    ("PERIPHERAL NEUROPATHY","G62.9","CHRONIC",0.4),
    ("IRRITABLE BOWEL SYNDROME","K58.9","CHRONIC",0.05),
    ("TRAUMATIC BRAIN INJURY RESIDUALS","S06.9XAS","CHRONIC",0.9),
    ("ADJUSTMENT DISORDER","F43.20","CHRONIC",0.2),
    ("OSTEOPOROSIS","M81.0","CHRONIC",0.05),
    ("FIBROMYALGIA","M79.7","CHRONIC",0.1),
    ("CHRONIC HEPATITIS C","B18.2","CHRONIC",0.3),
    ("BENIGN PROSTATIC HYPERPLASIA","N40.0","CHRONIC",0.05),
    ("IRON DEFICIENCY ANEMIA","D50.9","CHRONIC",0.05),
    ("GOUT","M10.9","CHRONIC",0.1),
    ("CERVICAL SPONDYLOSIS","M47.812","CHRONIC",0.3),
    ("CHRONIC INSOMNIA","G47.00","CHRONIC",0.2),
    ("CHRONIC PAIN SYNDROME","G89.29","CHRONIC",0.5),
    ("PERIPHERAL VASCULAR DISEASE","I73.9","CHRONIC",0.1),
    ("PARKINSON DISEASE","G20","CHRONIC",0.05),
    ("ANXIETY DISORDER","F41.9","CHRONIC",0.2),
    ("CHRONIC FATIGUE SYNDROME","R53.82","CHRONIC",0.1),
    ("TYPE 2 DIABETES MELLITUS WITH NEUROPATHY","E11.40","CHRONIC",0.1),
    ("CHRONIC OBSTRUCTIVE PULMONARY DISEASE WITH ACUTE EXACERBATION","J44.1","CHRONIC",0.2),
    ("DIABETIC CHRONIC KIDNEY DISEASE STAGE 4","E11.22","CHRONIC",0.05),
    ("SUBSTANCE USE DISORDER","F19.20","CHRONIC",0.4),
]

# ── Acute / injury conditions (assigned to specific patient profiles) ─────
ACUTE_PROBLEM_POOL = [
    # Fractures
    ("CLOSED FRACTURE OF LEFT TIBIA","S82.102A","ACUTE",0.7),
    ("CLOSED FRACTURE OF RIGHT TIBIA AND FIBULA","S82.101A","ACUTE",0.7),
    ("CLOSED FRACTURE OF LEFT FEMORAL NECK","S72.002A","ACUTE",0.3),
    ("CLOSED FRACTURE OF RIGHT FEMORAL NECK","S72.001A","ACUTE",0.3),
    ("CLOSED FRACTURE OF RIGHT DISTAL RADIUS","S52.501A","ACUTE",0.5),
    ("CLOSED FRACTURE OF LEFT ANKLE BIMALLEOLAR","S82.842A","ACUTE",0.6),
    ("CLOSED FRACTURE OF RIGHT ANKLE TRIMALLEOLAR","S82.851A","ACUTE",0.6),
    ("CLOSED FRACTURE OF LEFT PROXIMAL HUMERUS","S42.012A","ACUTE",0.5),
    ("CLOSED FRACTURE OF RIGHT DISTAL FEMUR","S72.401A","ACUTE",0.5),
    ("CLOSED FRACTURE OF LEFT PATELLA","S82.002A","ACUTE",0.6),
    ("COMPRESSION FRACTURE L1 VERTEBRA","S32.019A","ACUTE",0.5),
    ("CLOSED FRACTURE OF RIGHT CLAVICLE","S42.001A","ACUTE",0.4),
    # Other acute
    ("ACUTE CEREBROVASCULAR ACCIDENT","I63.9","ACUTE",0.2),
    ("ACUTE MYOCARDIAL INFARCTION","I21.9","ACUTE",0.2),
    ("PULMONARY EMBOLISM","I26.99","ACUTE",0.1),
    ("TRAUMATIC BELOW-KNEE AMPUTATION LEFT","S88.112A","ACUTE",0.9),
    ("ACHILLES TENDON RUPTURE RIGHT","S86.012A","ACUTE",0.6),
    ("ANTERIOR CRUCIATE LIGAMENT TEAR RIGHT KNEE","S83.511A","ACUTE",0.6),
    ("ROTATOR CUFF TEAR LEFT SHOULDER","M75.102","ACUTE",0.4),
    ("LUMBAR DISC HERNIATION L4-L5","M51.16","ACUTE",0.4),
]

# ICD10 codes that map to mental health problems (for MH screening correlation)
MH_ICD10S = {"F43.10","F33.0","F41.1","F43.20","F41.9","G47.00","F19.20"}
# ICD10 codes for cardiac problems (for specialist correlation)
CARDIAC_ICD10S = {"I25.10","I50.9","I48.91","I73.9","I21.9"}
# ICD10 codes for diabetes/CKD/CHF (for diet order correlation)
DIET_ICD10S = {"E11.9","I50.9","N18.3","E11.22","E11.40"}
# ICD10 codes for fractures (for ortho/PT/surgery correlation)
FRACTURE_ICD10S = {"S82.102A","S82.101A","S72.002A","S72.001A","S52.501A","S82.842A",
                   "S82.851A","S42.012A","S72.401A","S82.002A","S32.019A","S42.001A"}
# ICD10 codes for orthopedic/PT conditions
ORTHO_PT_ICD10S = FRACTURE_ICD10S | {"S88.112A","S86.012A","S83.511A","M75.102","M51.16",
                   "M17.0","M25.569","M06.9","M51.36","M54.5","M47.812"}

# ── Clinical patient profiles (coherent multi-condition stories) ──────────
# Each profile is a tuple: (name, problems_list, extra_meds_indices, has_surgery, has_pt)
# ~40% of patients get assigned a profile; the rest are random.
CLINICAL_PROFILES = [
    # Profile: Broken leg veteran - tibial fracture, ORIF, PT rehab
    {
        "name": "TIBIAL_FRACTURE",
        "problems": [
            ("CLOSED FRACTURE OF LEFT TIBIA","S82.102A","ACUTE",0.9),
            ("CHRONIC LOW BACK PAIN","M54.5","CHRONIC",0.5),
            ("POST-TRAUMATIC STRESS DISORDER","F43.10","CHRONIC",0.8),
        ],
        "surgery": "ORIF_TIBIA",
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: Hip fracture elderly veteran - fragility fracture, hemiarthroplasty
    {
        "name": "HIP_FRACTURE",
        "problems": [
            ("CLOSED FRACTURE OF LEFT FEMORAL NECK","S72.002A","ACUTE",0.3),
            ("OSTEOPOROSIS","M81.0","CHRONIC",0.05),
            ("TYPE 2 DIABETES MELLITUS","E11.9","CHRONIC",0.1),
            ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
        ],
        "surgery": "HEMIARTHROPLASTY",
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: Ankle fracture - bimalleolar, ORIF, PT
    {
        "name": "ANKLE_FRACTURE",
        "problems": [
            ("CLOSED FRACTURE OF LEFT ANKLE BIMALLEOLAR","S82.842A","ACUTE",0.6),
            ("CHRONIC PAIN SYNDROME","G89.29","CHRONIC",0.5),
            ("OBSTRUCTIVE SLEEP APNEA","G47.33","CHRONIC",0.1),
        ],
        "surgery": "ORIF_ANKLE",
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: Complex diabetic - DM2 + CKD + neuropathy + retinopathy
    {
        "name": "COMPLEX_DIABETIC",
        "problems": [
            ("TYPE 2 DIABETES MELLITUS","E11.9","CHRONIC",0.1),
            ("TYPE 2 DIABETES MELLITUS WITH NEUROPATHY","E11.40","CHRONIC",0.1),
            ("CHRONIC KIDNEY DISEASE STAGE 3","N18.3","CHRONIC",0.05),
            ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
            ("PERIPHERAL NEUROPATHY","G62.9","CHRONIC",0.4),
        ],
        "surgery": None,
        "pt_consult": False,
        "ortho_consult": False,
    },
    # Profile: Polytrauma veteran - TBI + PTSD + chronic pain + hearing loss
    {
        "name": "POLYTRAUMA",
        "problems": [
            ("TRAUMATIC BRAIN INJURY RESIDUALS","S06.9XAS","CHRONIC",0.9),
            ("POST-TRAUMATIC STRESS DISORDER","F43.10","CHRONIC",0.8),
            ("CHRONIC PAIN SYNDROME","G89.29","CHRONIC",0.5),
            ("BILATERAL HEARING LOSS","H91.93","CHRONIC",0.7),
            ("TINNITUS","H93.19","CHRONIC",0.8),
            ("CHRONIC LOW BACK PAIN","M54.5","CHRONIC",0.5),
        ],
        "surgery": None,
        "pt_consult": True,
        "ortho_consult": False,
    },
    # Profile: Cardiac complexity - CAD + CHF + AFib + PVD
    {
        "name": "CARDIAC_COMPLEX",
        "problems": [
            ("CORONARY ARTERY DISEASE","I25.10","CHRONIC",0.2),
            ("CONGESTIVE HEART FAILURE","I50.9","CHRONIC",0.1),
            ("ATRIAL FIBRILLATION","I48.91","CHRONIC",0.05),
            ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
            ("HYPERLIPIDEMIA","E78.5","CHRONIC",0.1),
        ],
        "surgery": "CABG",
        "pt_consult": False,
        "ortho_consult": False,
    },
    # Profile: ACL tear - sports injury, reconstruction, PT rehab
    {
        "name": "ACL_TEAR",
        "problems": [
            ("ANTERIOR CRUCIATE LIGAMENT TEAR RIGHT KNEE","S83.511A","ACUTE",0.6),
            ("CHRONIC KNEE PAIN","M25.569","CHRONIC",0.5),
            ("GENERALIZED ANXIETY DISORDER","F41.1","CHRONIC",0.2),
        ],
        "surgery": "ACL_RECON",
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: Wrist fracture - distal radius, casting or ORIF
    {
        "name": "WRIST_FRACTURE",
        "problems": [
            ("CLOSED FRACTURE OF RIGHT DISTAL RADIUS","S52.501A","ACUTE",0.5),
            ("OSTEOPOROSIS","M81.0","CHRONIC",0.05),
            ("HYPOTHYROIDISM","E03.9","CHRONIC",0.05),
        ],
        "surgery": "ORIF_WRIST",
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: COPD complexity - COPD + sleep apnea + CHF
    {
        "name": "PULMONARY_COMPLEX",
        "problems": [
            ("COPD","J44.1","CHRONIC",0.3),
            ("OBSTRUCTIVE SLEEP APNEA","G47.33","CHRONIC",0.1),
            ("CONGESTIVE HEART FAILURE","I50.9","CHRONIC",0.1),
            ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
            ("MAJOR DEPRESSIVE DISORDER","F33.0","CHRONIC",0.3),
        ],
        "surgery": None,
        "pt_consult": False,
        "ortho_consult": False,
    },
    # Profile: Below-knee amputation - trauma, prosthetics, PT
    {
        "name": "BKA",
        "problems": [
            ("TRAUMATIC BELOW-KNEE AMPUTATION LEFT","S88.112A","ACUTE",0.9),
            ("CHRONIC PAIN SYNDROME","G89.29","CHRONIC",0.5),
            ("POST-TRAUMATIC STRESS DISORDER","F43.10","CHRONIC",0.8),
            ("MAJOR DEPRESSIVE DISORDER","F33.0","CHRONIC",0.3),
        ],
        "surgery": "BKA",
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: Bilateral knee OA - both knees, TKA, PT rehab
    {
        "name": "BILATERAL_KNEE_OA",
        "problems": [
            ("OSTEOARTHRITIS OF BOTH KNEES","M17.0","CHRONIC",0.1),
            ("CHRONIC KNEE PAIN","M25.569","CHRONIC",0.5),
            ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
            ("TYPE 2 DIABETES MELLITUS","E11.9","CHRONIC",0.1),
        ],
        "surgery": "TKA",
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: Spine/back pain - DDD + herniation + chronic pain
    {
        "name": "SPINE_COMPLEX",
        "problems": [
            ("LUMBAR DEGENERATIVE DISC DISEASE","M51.36","CHRONIC",0.4),
            ("LUMBAR DISC HERNIATION L4-L5","M51.16","ACUTE",0.4),
            ("CHRONIC LOW BACK PAIN","M54.5","CHRONIC",0.5),
            ("CERVICAL SPONDYLOSIS","M47.812","CHRONIC",0.3),
        ],
        "surgery": None,
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: Achilles rupture - surgical repair, PT
    {
        "name": "ACHILLES_RUPTURE",
        "problems": [
            ("ACHILLES TENDON RUPTURE RIGHT","S86.012A","ACUTE",0.6),
            ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
        ],
        "surgery": "ACHILLES_REPAIR",
        "pt_consult": True,
        "ortho_consult": True,
    },
    # Profile: Stroke rehabilitation
    {
        "name": "STROKE_REHAB",
        "problems": [
            ("ACUTE CEREBROVASCULAR ACCIDENT","I63.9","ACUTE",0.2),
            ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
            ("ATRIAL FIBRILLATION","I48.91","CHRONIC",0.05),
            ("TYPE 2 DIABETES MELLITUS","E11.9","CHRONIC",0.1),
            ("MAJOR DEPRESSIVE DISORDER","F33.0","CHRONIC",0.3),
        ],
        "surgery": None,
        "pt_consult": True,
        "ortho_consult": False,
    },
    # Profile: Mental health complexity
    {
        "name": "MH_COMPLEX",
        "problems": [
            ("POST-TRAUMATIC STRESS DISORDER","F43.10","CHRONIC",0.8),
            ("MAJOR DEPRESSIVE DISORDER","F33.0","CHRONIC",0.3),
            ("SUBSTANCE USE DISORDER","F19.20","CHRONIC",0.4),
            ("CHRONIC INSOMNIA","G47.00","CHRONIC",0.2),
            ("CHRONIC PAIN SYNDROME","G89.29","CHRONIC",0.5),
        ],
        "surgery": None,
        "pt_consult": False,
        "ortho_consult": False,
    },
    # Profile: Rotator cuff repair
    {
        "name": "ROTATOR_CUFF",
        "problems": [
            ("ROTATOR CUFF TEAR LEFT SHOULDER","M75.102","ACUTE",0.4),
            ("CHRONIC PAIN SYNDROME","G89.29","CHRONIC",0.5),
            ("ESSENTIAL HYPERTENSION","I10","CHRONIC",0.3),
        ],
        "surgery": "ROTATOR_CUFF_REPAIR",
        "pt_consult": True,
        "ortho_consult": True,
    },
]

# Medication pool: (drug_name, dose, route, schedule, sig, days, qty, refills)
MED_POOL = [
    ("LISINOPRIL 10MG TAB","10MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("LISINOPRIL 20MG TAB","20MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("METFORMIN 500MG TAB","500MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("METFORMIN 1000MG TAB","1000MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("SERTRALINE 50MG TAB","50MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("SERTRALINE 100MG TAB","100MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("AMLODIPINE 5MG TAB","5MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("AMLODIPINE 10MG TAB","10MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("ATORVASTATIN 20MG TAB","20MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("ATORVASTATIN 40MG TAB","40MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("OMEPRAZOLE 20MG CAP","20MG","ORAL","DAILY","TAKE ONE CAPSULE BY MOUTH DAILY BEFORE BREAKFAST",90,90,3),
    ("GABAPENTIN 300MG CAP","300MG","ORAL","TID","TAKE ONE CAPSULE BY MOUTH THREE TIMES DAILY",90,270,3),
    ("GABAPENTIN 600MG CAP","600MG","ORAL","TID","TAKE ONE CAPSULE BY MOUTH THREE TIMES DAILY",90,270,3),
    ("PRAZOSIN 1MG CAP","1MG","ORAL","QHS","TAKE ONE CAPSULE BY MOUTH AT BEDTIME",90,90,3),
    ("PRAZOSIN 2MG CAP","2MG","ORAL","QHS","TAKE ONE CAPSULE BY MOUTH AT BEDTIME",90,90,3),
    ("ALBUTEROL INHALER","2 PUFFS","INHALATION","Q4-6H PRN","INHALE 2 PUFFS EVERY 4 TO 6 HOURS AS NEEDED",30,1,5),
    ("SUMATRIPTAN 50MG TAB","50MG","ORAL","PRN","TAKE ONE TABLET AT ONSET OF MIGRAINE",30,9,5),
    ("FUROSEMIDE 40MG TAB","40MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("CARVEDILOL 12.5MG TAB","12.5MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("WARFARIN 5MG TAB","5MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("DULOXETINE 60MG CAP","60MG","ORAL","DAILY","TAKE ONE CAPSULE BY MOUTH DAILY",90,90,3),
    ("TIOTROPIUM INHALER","18MCG","INHALATION","DAILY","INHALE ONE CAPSULE DAILY USING HANDIHALER",30,30,5),
    ("METHOTREXATE 7.5MG TAB","7.5MG","ORAL","WEEKLY","TAKE ONE TABLET BY MOUTH ONCE WEEKLY",30,4,3),
    ("BUSPIRONE 10MG TAB","10MG","ORAL","TID","TAKE ONE TABLET BY MOUTH THREE TIMES DAILY",90,270,3),
    ("TAMOXIFEN 20MG TAB","20MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("HYDROXYCHLOROQUINE 200MG TAB","200MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("NAPROXEN 500MG TAB","500MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY WITH FOOD",90,180,3),
    ("TOPIRAMATE 25MG TAB","25MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("COLCHICINE 0.6MG TAB","0.6MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("LEVOTHYROXINE 75MCG TAB","75MCG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH EACH MORNING ON EMPTY STOMACH",90,90,3),
    ("TAMSULOSIN 0.4MG CAP","0.4MG","ORAL","DAILY","TAKE ONE CAPSULE BY MOUTH DAILY 30 MINUTES AFTER SAME MEAL",90,90,3),
    ("DONEPEZIL 10MG TAB","10MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("CARBIDOPA-LEVODOPA 25-100MG TAB","25-100MG","ORAL","TID","TAKE ONE TABLET BY MOUTH THREE TIMES DAILY",90,270,3),
    ("IBUPROFEN 400MG TAB","400MG","ORAL","TID PRN","TAKE ONE TABLET BY MOUTH THREE TIMES DAILY AS NEEDED",90,270,3),
    ("ACETAMINOPHEN 500MG TAB","500MG","ORAL","Q6H PRN","TAKE ONE TABLET BY MOUTH EVERY 6 HOURS AS NEEDED FOR PAIN",90,120,3),
    ("DICYCLOMINE 20MG CAP","20MG","ORAL","QID","TAKE ONE CAPSULE BY MOUTH FOUR TIMES DAILY",90,360,3),
    ("FERROUS SULFATE 325MG TAB","325MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY WITH FOOD",90,90,3),
    ("OXYCODONE 5MG TAB","5MG","ORAL","Q6H","TAKE ONE TABLET BY MOUTH EVERY 6 HOURS",30,120,0),
    ("ALENDRONATE 70MG TAB","70MG","ORAL","WEEKLY","TAKE ONE TABLET BY MOUTH ONCE WEEKLY ON EMPTY STOMACH",30,4,3),
    ("MELOXICAM 15MG TAB","15MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY WITH FOOD",90,90,3),
    ("LOSARTAN 50MG TAB","50MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("PANTOPRAZOLE 40MG TAB","40MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY BEFORE BREAKFAST",90,90,3),
    ("TRAZODONE 50MG TAB","50MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("MONTELUKAST 10MG TAB","10MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("CLOPIDOGREL 75MG TAB","75MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
    ("METOPROLOL 25MG TAB","25MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("METOPROLOL 50MG TAB","50MG","ORAL","BID","TAKE ONE TABLET BY MOUTH TWICE DAILY",90,180,3),
    ("SIMVASTATIN 20MG TAB","20MG","ORAL","QHS","TAKE ONE TABLET BY MOUTH AT BEDTIME",90,90,3),
    ("FLUOXETINE 20MG CAP","20MG","ORAL","DAILY","TAKE ONE CAPSULE BY MOUTH DAILY",90,90,3),
    ("CITALOPRAM 20MG TAB","20MG","ORAL","DAILY","TAKE ONE TABLET BY MOUTH DAILY",90,90,3),
]

# Lab test pools
LAB_TESTS = {
    "GLUCOSE":     ("mg/dL","70","100", lambda: random.choice([random.randint(70,100), random.randint(101,180)])),
    "HBA1C":       ("%","4.0","5.6",    lambda: round(random.uniform(5.0, 9.5), 1)),
    "CREATININE":  ("mg/dL","0.7","1.3", lambda: round(random.uniform(0.6, 2.5), 1)),
    "BUN":         ("mg/dL","7","20",    lambda: random.randint(8, 35)),
    "POTASSIUM":   ("mEq/L","3.5","5.0", lambda: round(random.uniform(3.3, 5.5), 1)),
    "SODIUM":      ("mEq/L","136","145", lambda: random.randint(130, 148)),
    "CBC WBC":     ("K/cmm","4.5","11.0",lambda: round(random.uniform(3.5, 12.0), 1)),
    "HEMOGLOBIN":  ("g/dL","12.0","16.0",lambda: round(random.uniform(9.0, 17.5), 1)),
    "PLATELET COUNT":("K/cmm","150","400",lambda: random.randint(100, 450)),
    "TOTAL CHOLESTEROL":("mg/dL","0","200",lambda: random.randint(140, 280)),
    "LDL":         ("mg/dL","0","100",   lambda: random.randint(60, 180)),
    "TRIGLYCERIDES":("mg/dL","0","150",  lambda: random.randint(80, 300)),
    "ALT":         ("U/L","7","56",      lambda: random.randint(10, 90)),
    "AST":         ("U/L","10","40",     lambda: random.randint(10, 85)),
    "TSH":         ("mIU/L","0.4","4.0", lambda: round(random.uniform(0.3, 8.0), 1)),
    "INR":         ("ratio","2.0","3.0", lambda: round(random.uniform(1.5, 3.5), 1)),
    "BNP":         ("pg/mL","0","100",   lambda: random.choice([random.randint(20,100), random.randint(200,900)])),
    "PSA":         ("ng/mL","0","4.0",   lambda: round(random.uniform(0.5, 12.0), 1)),
    "ESR":         ("mm/hr","0","20",    lambda: random.randint(5, 55)),
    "CRP":         ("mg/L","0","1.0",    lambda: round(random.uniform(0.2, 5.0), 1)),
    "VITAMIN D":   ("ng/mL","30","100",  lambda: random.randint(12, 65)),
    "CALCIUM":     ("mg/dL","8.5","10.5",lambda: round(random.uniform(8.0, 11.0), 1)),
    "URIC ACID":   ("mg/dL","3.0","7.0", lambda: round(random.uniform(3.0, 10.0), 1)),
    "GFR":         ("mL/min","60","120", lambda: random.randint(15, 120)),
    "IRON":        ("mcg/dL","60","170", lambda: random.randint(25, 200)),
    "FERRITIN":    ("ng/mL","12","150",  lambda: random.randint(8, 200)),
    "ALBUMIN":     ("g/dL","3.5","5.0",  lambda: round(random.uniform(2.5, 5.0), 1)),
    "FREE T4":     ("ng/dL","0.8","1.8", lambda: round(random.uniform(0.6, 2.0), 1)),
}

VITAL_TYPES = ["BLOOD PRESSURE","PULSE","TEMPERATURE","RESPIRATION","WEIGHT","HEIGHT","PULSE OXIMETRY","PAIN"]
PAIN_LOCATIONS = ["HEAD","KNEE","BACK","JOINTS","FEET","NECK","SHOULDER","HIP","CHEST","ABDOMEN"]

ORDER_TYPES_PHARMACY = [
    "LISINOPRIL 10MG TAB","LISINOPRIL 20MG TAB","METFORMIN 500MG TAB","METFORMIN 1000MG TAB",
    "SERTRALINE 50MG TAB","SERTRALINE 100MG TAB","AMLODIPINE 5MG TAB","AMLODIPINE 10MG TAB",
    "ATORVASTATIN 20MG TAB","GABAPENTIN 300MG CAP","OMEPRAZOLE 20MG CAP","PRAZOSIN 1MG CAP",
    "ALBUTEROL INHALER","SUMATRIPTAN 50MG TAB","FUROSEMIDE 40MG TAB","CARVEDILOL 12.5MG TAB",
    "WARFARIN 5MG TAB","DULOXETINE 60MG CAP","TIOTROPIUM INHALER","BUSPIRONE 10MG TAB",
    "ACETAMINOPHEN 500MG TAB","NAPROXEN 500MG TAB","TOPIRAMATE 25MG TAB","HYDROXYCHLOROQUINE 200MG TAB",
    "LOSARTAN 50MG TAB","METOPROLOL 25MG TAB","CLOPIDOGREL 75MG TAB","PANTOPRAZOLE 40MG TAB",
]
ORDER_TYPES_LAB = [
    "COMPREHENSIVE METABOLIC PANEL","BASIC METABOLIC PANEL","CBC WITH DIFFERENTIAL",
    "LIPID PANEL","HEMOGLOBIN A1C","THYROID PANEL","HEPATIC FUNCTION PANEL",
    "URINALYSIS","PSA","INR","BNP","VITAMIN D LEVEL",
]
ORDER_TYPES_CONSULT = [
    "PSYCHOLOGY CONSULT","ORTHOPEDIC CONSULT","CARDIOLOGY CONSULT","PULMONARY CONSULT",
    "NEUROLOGY CONSULT","PAIN MANAGEMENT CONSULT","ENDOCRINOLOGY CONSULT",
]

CONSULT_SERVICES = [
    "CARDIOLOGY","ORTHOPEDICS","PSYCHOLOGY","PULMONARY","RHEUMATOLOGY","NEUROLOGY",
    "ONCOLOGY","NEPHROLOGY","HEPATOLOGY","PAIN MANAGEMENT","UROLOGY","GERIATRICS",
    "ENDOCRINOLOGY","SLEEP MEDICINE","GASTROENTEROLOGY","DERMATOLOGY",
    "PHYSICAL THERAPY","AUDIOLOGY","OPHTHALMOLOGY","PODIATRY","NUTRITION","SOCIAL WORK",
]
CONSULT_REASONS = {
    "CARDIOLOGY": ["Evaluate for coronary artery disease.","CHF exacerbation management.","Arrhythmia evaluation."],
    "ORTHOPEDICS": ["Evaluate joint pain and limited ROM.","Post-injury evaluation.","Arthritis management."],
    "PSYCHOLOGY": ["PTSD evaluation and treatment.","Depression management.","Anxiety assessment."],
    "PULMONARY": ["COPD management and rehab referral.","Dyspnea evaluation.","Pulmonary function testing."],
    "RHEUMATOLOGY": ["Evaluate joint swelling and elevated ESR.","Autoimmune workup.","RA management."],
    "NEUROLOGY": ["TBI follow-up evaluation.","Seizure evaluation.","Neuropathy assessment."],
    "ONCOLOGY": ["Cancer follow-up and surveillance.","New mass evaluation.","Treatment planning."],
    "NEPHROLOGY": ["CKD management and monitoring.","Declining GFR evaluation.","Electrolyte management."],
    "HEPATOLOGY": ["Hepatitis C treatment evaluation.","Cirrhosis management.","Liver function decline."],
    "PAIN MANAGEMENT": ["Chronic pain refractory to treatment.","Multimodal pain approach.","Medication review."],
    "UROLOGY": ["Elevated PSA evaluation.","BPH management.","Urinary symptoms."],
    "GERIATRICS": ["Cognitive decline assessment.","Falls risk evaluation.","Comprehensive geriatric assessment."],
    "ENDOCRINOLOGY": ["Uncontrolled diabetes management.","Thyroid disorder evaluation.","Metabolic syndrome."],
    "SLEEP MEDICINE": ["Suspected sleep apnea.","CPAP titration.","Insomnia evaluation."],
    "GASTROENTEROLOGY": ["IBS management.","GI bleeding evaluation.","GERD refractory to PPI."],
    "DERMATOLOGY": ["Suspicious skin lesion evaluation.","Chronic rash assessment.","Skin cancer screening."],
    "PHYSICAL THERAPY": ["Post-surgical rehabilitation.","Core strengthening for back pain.","ROM restoration."],
    "AUDIOLOGY": ["Hearing loss evaluation.","Hearing aid fitting.","Tinnitus assessment."],
    "OPHTHALMOLOGY": ["Diabetic retinopathy screening.","Vision changes evaluation.","Glaucoma screening."],
    "PODIATRY": ["Diabetic foot exam.","Foot pain evaluation.","Nail care."],
    "NUTRITION": ["Diabetes diet counseling.","Weight management.","Renal diet education."],
    "SOCIAL WORK": ["Benefits counseling.","Housing assistance.","Caregiver support."],
}

SURGERY_PROCEDURES = [
    ("LEFT KNEE ARTHROPLASTY","GENERAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS LEFT KNEE",
     ["Procedure: Total left knee arthroplasty","Findings: Severe tricompartmental osteoarthritis","Implant: Stryker Triathlon cemented TKA","EBL: 250ml. No complications."]),
    ("RIGHT KNEE ARTHROPLASTY","GENERAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS RIGHT KNEE",
     ["Procedure: Total right knee arthroplasty","Findings: Severe medial compartment osteoarthritis","Implant: Smith & Nephew Genesis II","EBL: 200ml. No complications."]),
    ("RIGHT TOTAL HIP ARTHROPLASTY","SPINAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS RIGHT HIP",
     ["Procedure: Posterior approach right total hip arthroplasty","Implant: Stryker Accolade II stem, Trident cup","EBL: 300ml. Hip stable in all planes."]),
    ("LEFT TOTAL HIP ARTHROPLASTY","SPINAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS LEFT HIP",
     ["Procedure: Anterior approach left total hip arthroplasty","Implant: DePuy Pinnacle cup, Corail stem","EBL: 275ml. No complications."]),
    ("CORONARY ARTERY BYPASS GRAFT X3","GENERAL","CARDIAC SURGERY","THREE-VESSEL CORONARY ARTERY DISEASE",
     ["Procedure: CABG x3 (LIMA to LAD, SVG to RCA, SVG to OM1)","CPB time: 95 minutes. Cross-clamp: 62 minutes.","EBL: 600ml. Transferred to SICU in stable condition."]),
    ("LEFT BREAST LUMPECTOMY","LOCAL WITH SEDATION","GENERAL SURGERY","BREAST CARCINOMA LEFT",
     ["Procedure: Left breast lumpectomy with sentinel lymph node biopsy","Findings: 1.8cm mass at 2 o'clock position","Sentinel node negative. Margins clear. EBL: 50ml."]),
    ("LAPAROSCOPIC CHOLECYSTECTOMY","GENERAL","GENERAL SURGERY","SYMPTOMATIC CHOLELITHIASIS",
     ["Procedure: Laparoscopic cholecystectomy","Findings: Chronically inflamed gallbladder with multiple stones","EBL: 25ml. No complications. Same-day discharge."]),
    ("RIGHT ROTATOR CUFF REPAIR","GENERAL","ORTHOPEDICS","FULL-THICKNESS ROTATOR CUFF TEAR",
     ["Procedure: Arthroscopic right rotator cuff repair","Findings: 2.5cm full-thickness supraspinatus tear","Double-row repair with suture anchors. EBL: minimal."]),
    ("TRANSURETHRAL RESECTION OF PROSTATE","SPINAL","UROLOGY","BENIGN PROSTATIC HYPERPLASIA",
     ["Procedure: TURP","Findings: Markedly enlarged prostate, 80 grams","EBL: 100ml. Foley catheter placed."]),
    ("LAPAROSCOPIC APPENDECTOMY","GENERAL","GENERAL SURGERY","ACUTE APPENDICITIS",
     ["Procedure: Laparoscopic appendectomy","Findings: Inflamed appendix without perforation","EBL: 15ml. No complications."]),
    ("PERMANENT PACEMAKER INSERTION","LOCAL WITH SEDATION","CARDIAC SURGERY","COMPLETE HEART BLOCK",
     ["Procedure: Dual-chamber permanent pacemaker implantation","Device: Medtronic Azure XT DR MRI","Lead positions: RA appendage and RV apex. Thresholds excellent."]),
    ("INGUINAL HERNIA REPAIR","GENERAL","GENERAL SURGERY","RIGHT INGUINAL HERNIA",
     ["Procedure: Laparoscopic right inguinal hernia repair with mesh","Findings: Direct inguinal hernia","Mesh: Bard 3DMax. EBL: minimal."]),
    ("CAROTID ENDARTERECTOMY","GENERAL","VASCULAR SURGERY","CAROTID STENOSIS 80%",
     ["Procedure: Right carotid endarterectomy","Findings: 80% stenosis right ICA","Patch angioplasty performed. EBL: 150ml."]),
    ("AORTIC VALVE REPLACEMENT","GENERAL","CARDIAC SURGERY","SEVERE AORTIC STENOSIS",
     ["Procedure: Aortic valve replacement with bioprosthetic valve","CPB time: 78 minutes. Cross-clamp: 55 minutes.","Edwards Inspiris Resilia 23mm valve placed. EBL: 500ml."]),
]

# Profile-linked surgery procedures — keyed by profile surgery name
PROFILE_SURGERY_MAP = {
    "ORIF_TIBIA": ("ORIF LEFT TIBIA","GENERAL","ORTHOPEDICS","CLOSED FRACTURE LEFT TIBIA",
     ["Procedure: Open reduction internal fixation of left tibial shaft fracture",
      "Findings: Comminuted transverse fracture of the left tibial diaphysis",
      "Hardware: Synthes tibial nail, 2 proximal locking screws, 2 distal locking screws",
      "Alignment restored under fluoroscopic guidance. EBL: 175ml.",
      "Leg placed in posterior splint. Weight-bearing restrictions discussed.",
      "Plan: Non-weight-bearing x6 weeks, then progressive weight-bearing with PT."]),
    "HEMIARTHROPLASTY": ("LEFT HIP HEMIARTHROPLASTY","SPINAL","ORTHOPEDICS","FEMORAL NECK FRACTURE LEFT HIP",
     ["Procedure: Cemented left hip hemiarthroplasty via posterolateral approach",
      "Findings: Displaced intracapsular femoral neck fracture, Garden IV",
      "Implant: Zimmer Austin Moore prosthesis, 47mm head, cemented",
      "Hip stable through full range of motion. Leg lengths equal.",
      "EBL: 350ml. Posterior hip precautions implemented.",
      "Plan: Weight-bearing as tolerated. PT to begin POD#1."]),
    "ORIF_ANKLE": ("ORIF LEFT ANKLE","GENERAL","ORTHOPEDICS","BIMALLEOLAR FRACTURE LEFT ANKLE",
     ["Procedure: Open reduction internal fixation left bimalleolar ankle fracture",
      "Findings: Displaced lateral malleolus fracture, transverse medial malleolus fracture",
      "Hardware: Lateral plate and screws, 2 medial malleolar screws",
      "Syndesmosis intact on stress testing. Anatomic reduction achieved.",
      "EBL: 100ml. Posterior splint applied.",
      "Plan: Non-weight-bearing x6 weeks, then CAM boot and PT."]),
    "CABG": ("CORONARY ARTERY BYPASS GRAFT X3","GENERAL","CARDIAC SURGERY","THREE-VESSEL CORONARY ARTERY DISEASE",
     ["Procedure: CABG x3 (LIMA to LAD, SVG to RCA, SVG to OM1)",
      "CPB time: 95 minutes. Cross-clamp: 62 minutes.",
      "EBL: 600ml. Transferred to SICU in stable condition.",
      "Plan: Cardiac rehab referral. Sternal precautions x8 weeks."]),
    "ACL_RECON": ("ACL RECONSTRUCTION RIGHT KNEE","GENERAL","ORTHOPEDICS","COMPLETE ACL TEAR RIGHT KNEE",
     ["Procedure: Arthroscopic ACL reconstruction right knee with BTB autograft",
      "Findings: Complete ACL tear. Intact menisci bilaterally. Grade II chondral changes medial femoral condyle.",
      "Graft: Bone-patellar tendon-bone autograft, 10mm width",
      "Femoral tunnel 30mm, tibial tunnel 35mm. Interference screw fixation both tunnels.",
      "Lachman and pivot shift negative post-fixation. Full ROM achieved.",
      "EBL: minimal. Hinged knee brace applied, locked in extension.",
      "Plan: Weight-bearing as tolerated in brace. PT protocol begins POD#1."]),
    "ORIF_WRIST": ("ORIF RIGHT DISTAL RADIUS","LOCAL WITH SEDATION","ORTHOPEDICS","DISTAL RADIUS FRACTURE RIGHT WRIST",
     ["Procedure: Open reduction internal fixation right distal radius via volar approach",
      "Findings: Dorsally displaced comminuted distal radius fracture",
      "Hardware: Synthes volar locking plate, 6 distal locking screws, 3 shaft screws",
      "Articular surface restored. Radiocarpal joint congruent on fluoroscopy.",
      "Median nerve decompressed. No signs of acute carpal tunnel syndrome.",
      "EBL: 25ml. Sugar-tong splint applied.",
      "Plan: Remove splint at 2 weeks, begin gentle ROM with OT/PT."]),
    "BKA": ("LEFT BELOW-KNEE AMPUTATION","GENERAL","ORTHOPEDICS","TRAUMATIC LEFT LOWER EXTREMITY INJURY",
     ["Procedure: Left transtibial amputation with posterior myocutaneous flap",
      "Findings: Non-salvageable traumatic injury to left lower extremity distal to knee",
      "Bone transected 14cm below tibial tuberosity. Fibula cut 2cm proximal to tibia.",
      "Anterior and posterior muscle groups shaped over bone end. Rigid dressing applied.",
      "EBL: 200ml. Drain placed.",
      "Plan: Prosthetics consult. PT for pre-prosthetic training. Psychology for adjustment counseling."]),
    "TKA": ("LEFT TOTAL KNEE ARTHROPLASTY","GENERAL","ORTHOPEDICS","SEVERE OSTEOARTHRITIS LEFT KNEE",
     ["Procedure: Total left knee arthroplasty via medial parapatellar approach",
      "Findings: Severe tricompartmental osteoarthritis with bone-on-bone medially",
      "Implant: Stryker Triathlon PS, cemented. Size 5 femoral, size 4 tibial, 10mm poly",
      "Patellar resurfacing performed. Tracking satisfactory.",
      "EBL: 250ml. TXA 1g IV given. Tourniquet time 65 minutes.",
      "Knee flexion to 110 degrees on table. Stable in all planes.",
      "Plan: Weight-bearing as tolerated. CPM machine. PT begins POD#1."]),
    "ACHILLES_REPAIR": ("ACHILLES TENDON REPAIR RIGHT","GENERAL","ORTHOPEDICS","COMPLETE ACHILLES TENDON RUPTURE",
     ["Procedure: Open repair right Achilles tendon",
      "Findings: Complete rupture of Achilles tendon 4cm proximal to calcaneal insertion",
      "Technique: Modified Krackow suture with #2 FiberWire. End-to-end repair achieved.",
      "Tendon in good apposition with ankle in 20 degrees plantarflexion.",
      "EBL: 30ml. Posterior splint applied in plantarflexion.",
      "Plan: Non-weight-bearing x4 weeks, then progressive WB in CAM boot. PT at 6 weeks."]),
    "ROTATOR_CUFF_REPAIR": ("ARTHROSCOPIC LEFT ROTATOR CUFF REPAIR","GENERAL","ORTHOPEDICS","LEFT ROTATOR CUFF TEAR",
     ["Procedure: Arthroscopic left rotator cuff repair with subacromial decompression",
      "Findings: Full-thickness 3cm tear of supraspinatus with retraction to glenoid rim",
      "Technique: Double-row suture bridge repair using 2 medial and 2 lateral anchors",
      "Biceps tenotomy performed due to significant fraying (>50%).",
      "Subacromial decompression and acromioplasty completed.",
      "EBL: minimal. Sling applied.",
      "Plan: Sling x6 weeks. Passive ROM only x6 weeks. PT protocol begins at 2 weeks."]),
}

RAD_PROCEDURES = [
    ("CHEST X-RAY PA AND LATERAL","GENERAL RADIOLOGY",
     ["CHEST X-RAY PA AND LATERAL","FINDINGS: Heart size upper limits of normal.","Lungs clear bilaterally. No infiltrates or effusions.","IMPRESSION: No acute cardiopulmonary disease."]),
    ("CHEST X-RAY PA","GENERAL RADIOLOGY",
     ["CHEST X-RAY PA","FINDINGS: Heart size normal. Lungs clear.","IMPRESSION: No acute disease."]),
    ("CT HEAD WITHOUT CONTRAST","CT SCAN",
     ["CT HEAD WITHOUT CONTRAST","FINDINGS: No acute intracranial hemorrhage.","No mass effect or midline shift.","IMPRESSION: No acute intracranial abnormality."]),
    ("CT CHEST WITH CONTRAST","CT SCAN",
     ["CT CHEST WITH CONTRAST","FINDINGS: No pulmonary embolism.","No pulmonary nodules or masses.","IMPRESSION: No acute findings."]),
    ("CT ABDOMEN AND PELVIS WITH CONTRAST","CT SCAN",
     ["CT ABDOMEN AND PELVIS WITH CONTRAST","FINDINGS: No lymphadenopathy. Kidneys unremarkable.","No free fluid or mass.","IMPRESSION: No acute intra-abdominal pathology."]),
    ("MRI LUMBAR SPINE WITHOUT CONTRAST","MRI",
     ["MRI LUMBAR SPINE WITHOUT CONTRAST","FINDINGS: Disc degeneration at L4-5 and L5-S1.","Mild to moderate central canal stenosis.","IMPRESSION: Multilevel degenerative disc disease."]),
    ("MRI CERVICAL SPINE WITHOUT CONTRAST","MRI",
     ["MRI CERVICAL SPINE WITHOUT CONTRAST","FINDINGS: Disc bulges at C5-6 and C6-7.","Mild foraminal narrowing bilaterally.","IMPRESSION: Cervical spondylosis."]),
    ("MRI RIGHT KNEE WITHOUT CONTRAST","MRI",
     ["MRI RIGHT KNEE WITHOUT CONTRAST","FINDINGS: Grade III signal in medial meniscus.","Moderate joint effusion.","IMPRESSION: Medial meniscus tear with OA changes."]),
    ("ECHOCARDIOGRAM","ULTRASOUND",
     ["TRANSTHORACIC ECHOCARDIOGRAM","FINDINGS: LVEF estimated at 55%. Normal wall motion.","No significant valvular disease.","IMPRESSION: Normal cardiac function."]),
    ("ULTRASOUND ABDOMEN","ULTRASOUND",
     ["ULTRASOUND ABDOMEN COMPLETE","FINDINGS: Liver normal in size and echotexture.","No gallstones. Kidneys unremarkable.","IMPRESSION: Normal abdominal ultrasound."]),
    ("X-RAY LUMBAR SPINE","GENERAL RADIOLOGY",
     ["X-RAY LUMBAR SPINE AP AND LATERAL","FINDINGS: Degenerative disc disease L4-5 and L5-S1.","No fracture or listhesis.","IMPRESSION: Degenerative changes."]),
    ("DEXA SCAN","NUCLEAR MEDICINE",
     ["DEXA BONE DENSITY SCAN","FINDINGS: Lumbar spine T-score -1.8. Femoral neck T-score -1.5.","IMPRESSION: Osteopenia."]),
    ("MAMMOGRAM BILATERAL","GENERAL RADIOLOGY",
     ["BILATERAL MAMMOGRAM","FINDINGS: No suspicious calcifications or masses.","IMPRESSION: BI-RADS 1 - Negative."]),
    ("X-RAY BOTH KNEES","GENERAL RADIOLOGY",
     ["X-RAY BOTH KNEES AP AND LATERAL","FINDINGS: Bilateral medial compartment joint space narrowing.","Osteophyte formation. No fracture.","IMPRESSION: Moderate bilateral knee osteoarthritis."]),
]

# Profile-linked radiology — keyed by profile name, list of studies per profile
PROFILE_RAD_MAP = {
    "TIBIAL_FRACTURE": [
        ("X-RAY LEFT TIBIA AND FIBULA","GENERAL RADIOLOGY",
         ["X-RAY LEFT TIBIA AND FIBULA AP AND LATERAL",
          "FINDINGS: Transverse fracture of the left tibial shaft at the junction of the middle and distal thirds.",
          "Approximately 5mm of displacement and 10 degrees of varus angulation.",
          "Fibula intact. No intra-articular extension.",
          "IMPRESSION: Displaced transverse fracture left tibial shaft. Surgical consultation recommended."]),
        ("X-RAY LEFT TIBIA AND FIBULA POST-OP","GENERAL RADIOLOGY",
         ["X-RAY LEFT TIBIA AND FIBULA POST-OPERATIVE",
          "FINDINGS: Status post intramedullary nailing of left tibial shaft fracture.",
          "Hardware in satisfactory position. Fracture fragments in acceptable alignment.",
          "No hardware complication. Early callus formation noted.",
          "IMPRESSION: Post-ORIF left tibia. Hardware intact. Healing in progress."]),
    ],
    "HIP_FRACTURE": [
        ("X-RAY LEFT HIP","GENERAL RADIOLOGY",
         ["X-RAY LEFT HIP AP AND FROG-LEG LATERAL",
          "FINDINGS: Displaced intracapsular fracture of the left femoral neck, Garden type IV.",
          "Shortening and external rotation of the left lower extremity noted.",
          "Diffuse osteopenia throughout the pelvis and proximal femora.",
          "IMPRESSION: Displaced left femoral neck fracture. Surgical intervention indicated."]),
        ("X-RAY LEFT HIP POST-OP","GENERAL RADIOLOGY",
         ["X-RAY LEFT HIP POST-OPERATIVE",
          "FINDINGS: Status post left hip hemiarthroplasty.",
          "Prosthesis in satisfactory position. No periprosthetic fracture.",
          "Cement mantle intact. Appropriate offset and leg length.",
          "IMPRESSION: Post-hemiarthroplasty left hip. Satisfactory position."]),
    ],
    "ANKLE_FRACTURE": [
        ("X-RAY LEFT ANKLE","GENERAL RADIOLOGY",
         ["X-RAY LEFT ANKLE AP, LATERAL, AND MORTISE",
          "FINDINGS: Oblique fracture of the lateral malleolus with 4mm displacement.",
          "Transverse fracture of the medial malleolus. Talar shift of 3mm.",
          "Posterior malleolus intact. No talar dome lesion.",
          "IMPRESSION: Displaced bimalleolar fracture left ankle with talar shift. Unstable injury."]),
        ("X-RAY LEFT ANKLE POST-OP","GENERAL RADIOLOGY",
         ["X-RAY LEFT ANKLE POST-OPERATIVE",
          "FINDINGS: ORIF of left bimalleolar ankle fracture.",
          "Lateral plate and screws in good position. Medial malleolar screws intact.",
          "Anatomic reduction of the ankle mortise. Syndesmosis congruent.",
          "IMPRESSION: Post-ORIF left ankle. Hardware intact. Anatomic alignment."]),
    ],
    "ACL_TEAR": [
        ("MRI RIGHT KNEE WITHOUT CONTRAST","MRI",
         ["MRI RIGHT KNEE WITHOUT CONTRAST",
          "FINDINGS: Complete disruption of the anterior cruciate ligament with edema and hemorrhage.",
          "Bone bruise pattern of lateral femoral condyle and posterolateral tibial plateau consistent with pivot shift mechanism.",
          "Medial and lateral menisci intact. MCL and LCL intact.",
          "Grade II chondral changes medial femoral condyle.",
          "Moderate joint effusion. No loose bodies.",
          "IMPRESSION: Complete ACL tear right knee. Bone bruise pattern. Intact menisci."]),
    ],
    "WRIST_FRACTURE": [
        ("X-RAY RIGHT WRIST","GENERAL RADIOLOGY",
         ["X-RAY RIGHT WRIST AP AND LATERAL",
          "FINDINGS: Dorsally displaced comminuted fracture of the right distal radius.",
          "Extension into the radiocarpal joint surface. Dorsal tilt of 25 degrees.",
          "Ulnar styloid fracture at tip. No carpal fracture or dislocation.",
          "IMPRESSION: Displaced comminuted intra-articular distal radius fracture, right."]),
        ("X-RAY RIGHT WRIST POST-OP","GENERAL RADIOLOGY",
         ["X-RAY RIGHT WRIST POST-OPERATIVE",
          "FINDINGS: Status post ORIF right distal radius with volar locking plate.",
          "Articular surface restored. Radial height, inclination, and tilt within normal limits.",
          "Hardware intact. No signs of complication.",
          "IMPRESSION: Post-ORIF right distal radius. Satisfactory alignment."]),
    ],
    "BILATERAL_KNEE_OA": [
        ("X-RAY BOTH KNEES","GENERAL RADIOLOGY",
         ["X-RAY BOTH KNEES STANDING AP AND LATERAL",
          "FINDINGS: Severe medial compartment joint space loss bilaterally, bone-on-bone.",
          "Large osteophytes along medial and lateral compartments.",
          "Moderate patellofemoral disease. Subchondral sclerosis and cyst formation.",
          "No fracture. Varus angulation approximately 8 degrees bilateral.",
          "IMPRESSION: Severe bilateral tricompartmental osteoarthritis. Surgical candidate."]),
    ],
    "SPINE_COMPLEX": [
        ("MRI LUMBAR SPINE WITHOUT CONTRAST","MRI",
         ["MRI LUMBAR SPINE WITHOUT CONTRAST",
          "FINDINGS: L4-5: Large left paracentral disc extrusion compressing the left L5 nerve root.",
          "Moderate central canal stenosis at L4-5. Bilateral facet arthropathy.",
          "L5-S1: Diffuse disc bulge with annular tear. Mild bilateral foraminal narrowing.",
          "L3-4: Mild disc bulge without stenosis.",
          "Conus terminates at L1-2. No abnormal enhancement.",
          "IMPRESSION: L4-5 disc extrusion with left L5 radiculopathy. Multilevel DDD."]),
    ],
    "ROTATOR_CUFF": [
        ("MRI LEFT SHOULDER WITHOUT CONTRAST","MRI",
         ["MRI LEFT SHOULDER WITHOUT CONTRAST",
          "FINDINGS: Full-thickness tear of the supraspinatus tendon measuring approximately 3cm AP x 2cm ML.",
          "Tendon retracted to the level of the glenoid rim. Moderate fatty infiltration (Goutallier 2).",
          "Infraspinatus and subscapularis intact. Long head of biceps tendon frayed (>50%).",
          "Moderate glenohumeral joint effusion. Type II acromion with spurring.",
          "IMPRESSION: Full-thickness supraspinatus tear with retraction. Biceps fraying. Type II acromion."]),
    ],
    "STROKE_REHAB": [
        ("CT HEAD WITHOUT CONTRAST","CT SCAN",
         ["CT HEAD WITHOUT CONTRAST",
          "FINDINGS: Acute hypodensity in the right middle cerebral artery territory",
          "involving the right frontal and parietal lobes consistent with acute infarction.",
          "No hemorrhagic transformation. Mild midline shift of 3mm to the left.",
          "No hydrocephalus.",
          "IMPRESSION: Acute right MCA territory infarct. No hemorrhage."]),
        ("MRI BRAIN WITH AND WITHOUT CONTRAST","MRI",
         ["MRI BRAIN WITH AND WITHOUT CONTRAST",
          "FINDINGS: Acute/subacute infarct in the right MCA territory involving",
          "the right frontal, parietal, and temporal lobes. DWI restricted diffusion confirmed.",
          "No hemorrhagic transformation. Left-sided neglect expected clinically.",
          "Patent right ICA and vertebrobasilar system on MRA.",
          "IMPRESSION: Acute right MCA territory infarct. No hemorrhagic conversion."]),
    ],
    "ACHILLES_RUPTURE": [
        ("MRI RIGHT ANKLE WITHOUT CONTRAST","MRI",
         ["MRI RIGHT ANKLE WITHOUT CONTRAST",
          "FINDINGS: Complete disruption of the Achilles tendon approximately 4cm proximal",
          "to the calcaneal insertion. Gap of approximately 2.5cm between the tendon stumps.",
          "Surrounding hemorrhage and edema in the pre-Achilles fat pad.",
          "No posterior tibial tendon or peroneal tendon abnormality.",
          "IMPRESSION: Complete Achilles tendon rupture, right."]),
    ],
    "BKA": [
        ("X-RAY LEFT TIBIA AND FIBULA","GENERAL RADIOLOGY",
         ["X-RAY LEFT TIBIA AND FIBULA",
          "FINDINGS: Severe comminuted fracture of the left distal tibia and fibula",
          "with extensive soft tissue injury. Open fracture pattern.",
          "Vascular compromise suspected based on clinical correlation.",
          "IMPRESSION: Severe comminuted open fracture left lower extremity. Non-salvageable."]),
    ],
}

# Note templates — generic (used when no profile-specific note is available)
NOTE_TEMPLATES = [
    ("PRIMARY CARE VISIT", [
        "SUBJECTIVE: Patient presents for routine follow-up of chronic conditions.",
        "Reports compliance with medications. Denies new complaints.",
        "Sleep adequate. Appetite normal. Activity level stable.",
        "",
        "OBJECTIVE:",
        "General: Well-appearing, no acute distress.",
        "HEENT: PERRL, EOMI, oropharynx clear.",
        "Cardiovascular: RRR, no murmurs, rubs, or gallops.",
        "Lungs: Clear to auscultation bilaterally.",
        "Abdomen: Soft, non-tender, non-distended.",
        "Extremities: No edema. Pedal pulses palpable bilaterally.",
        "",
        "ASSESSMENT AND PLAN:",
        "Chronic conditions stable on current regimen.",
        "Medications reconciled. No changes indicated.",
        "Age-appropriate screening up to date.",
        "Continue current management. Follow up in 3 months."]),
    ("PRIMARY CARE ANNUAL", [
        "ANNUAL COMPREHENSIVE EXAMINATION",
        "",
        "SUBJECTIVE: Patient presents for annual comprehensive exam.",
        "No new complaints. Functional status maintained.",
        "Reviews of systems negative except as documented in problem list.",
        "Current medications reviewed and reconciled with patient.",
        "",
        "OBJECTIVE:",
        "General: Well-nourished, well-developed, no acute distress.",
        "HEENT: Normal. TMs clear bilaterally. Dentition fair.",
        "Neck: Supple. No lymphadenopathy. Thyroid normal.",
        "Cardiovascular: RRR. No murmurs.",
        "Lungs: Clear bilaterally. Good air movement.",
        "Abdomen: Soft, non-tender. No organomegaly.",
        "Skin: No suspicious lesions. Skin cancer screening performed.",
        "Neuro: Alert, oriented x4. Cranial nerves intact. Strength 5/5.",
        "Musculoskeletal: Full ROM all major joints. No joint swelling.",
        "",
        "ASSESSMENT AND PLAN:",
        "1. Health maintenance current. All screening tests reviewed.",
        "2. Immunizations updated per schedule.",
        "3. Continue current medications without change.",
        "4. Follow up in 1 year for annual exam or sooner as needed."]),
    ("MENTAL HEALTH FOLLOW-UP", [
        "MENTAL HEALTH PROGRESS NOTE",
        "",
        "SUBJECTIVE: Patient reports stable mood since last visit.",
        "Sleep improved on current regimen. Getting 6-7 hours per night.",
        "Appetite fair. Denies suicidal or homicidal ideation.",
        "Engaging in weekly therapy sessions. Reports finding them helpful.",
        "Some ongoing hypervigilance and startle response noted.",
        "Nightmares occurring 1-2x per week, down from 4-5x at last visit.",
        "",
        "OBJECTIVE:",
        "Appearance: Well-groomed, appropriate dress.",
        "Behavior: Cooperative, good eye contact.",
        "Speech: Normal rate, rhythm, and volume.",
        "Mood: 'Better' Affect: Euthymic, congruent.",
        "Thought process: Linear, goal-directed.",
        "Thought content: No SI/HI. No delusions or hallucinations.",
        "Cognition: Alert, oriented x4.",
        "PHQ-9: Reviewed. GAD-7: Reviewed.",
        "",
        "ASSESSMENT AND PLAN:",
        "Symptoms improving on current medication regimen.",
        "Continue current psychotropic medications.",
        "Continue weekly psychotherapy.",
        "Safety plan reviewed and updated. Patient verbalizes plan.",
        "Follow up in 4 weeks."]),
    ("CARDIOLOGY FOLLOW-UP", [
        "CARDIOLOGY CLINIC NOTE",
        "",
        "SUBJECTIVE: Patient presents for cardiac follow-up.",
        "Denies chest pain, shortness of breath at rest, or palpitations.",
        "Can walk 2 blocks without symptoms. Orthopnea stable.",
        "Medication compliance good. No side effects reported.",
        "",
        "OBJECTIVE:",
        "General: No acute distress.",
        "JVP: Not elevated.",
        "Cardiovascular: Irregularly irregular rhythm. S1/S2 normal. No S3 or S4. Grade 2/6 systolic murmur at apex.",
        "Lungs: Clear bilaterally. No crackles or wheezes.",
        "Extremities: Trace pedal edema bilaterally.",
        "",
        "Labs: BNP 185 pg/mL. BMP stable. INR 2.4 (therapeutic).",
        "Echo (recent): LVEF 40%. Mild MR. Moderate LA dilation.",
        "",
        "ASSESSMENT AND PLAN:",
        "1. CHF: Stable NYHA Class II. Continue current HF regimen.",
        "2. Atrial fibrillation: Rate controlled. INR therapeutic.",
        "3. CAD: Continue statin and antiplatelet therapy.",
        "4. Repeat echo in 6 months. BNP in 3 months.",
        "5. Follow up in 3 months. Return sooner for worsening dyspnea, weight gain, or chest pain."]),
    ("ENDOCRINOLOGY FOLLOW-UP", [
        "ENDOCRINOLOGY CLINIC NOTE",
        "",
        "SUBJECTIVE: Patient presents for diabetes management follow-up.",
        "Home glucose readings reviewed: fasting 110-160, post-prandial 140-220.",
        "Denies hypoglycemic episodes. No polyuria or polydipsia.",
        "Following diabetic diet. Walking 20 minutes 3x/week.",
        "Last eye exam 6 months ago — no retinopathy. Foot exam today.",
        "",
        "OBJECTIVE:",
        "Foot exam: Monofilament intact bilaterally. Pedal pulses palpable.",
        "No foot ulcers or callus formation. Nails trimmed appropriately.",
        "",
        "Labs: HbA1c 7.8% (prior 8.2%). Creatinine 1.1. eGFR 72.",
        "Fasting glucose 148. Lipid panel: LDL 95, HDL 42, TG 180.",
        "",
        "ASSESSMENT AND PLAN:",
        "1. T2DM: A1C improving but not at goal. Increase metformin to 1000mg BID.",
        "2. Consider adding GLP-1 agonist if A1C not <7.5% in 3 months.",
        "3. Diabetic nephropathy: Stable. Continue ACE inhibitor.",
        "4. Diabetic foot care education reinforced.",
        "5. Recheck A1C, BMP, microalbumin in 3 months."]),
    ("PULMONOLOGY FOLLOW-UP", [
        "PULMONOLOGY CLINIC NOTE",
        "",
        "SUBJECTIVE: Patient presents for COPD management.",
        "Reports 1 exacerbation in the last 3 months requiring oral steroids.",
        "Cough productive of white sputum daily. Dyspnea on exertion stable.",
        "Using rescue inhaler 2-3x per week. CPAP compliance reviewed.",
        "Denies hemoptysis, fever, or weight loss.",
        "",
        "OBJECTIVE:",
        "General: Speaking in full sentences without pause.",
        "Lungs: Decreased breath sounds at bases. Mild expiratory wheeze bilaterally.",
        "No accessory muscle use. No cyanosis.",
        "SpO2: 93% on room air.",
        "",
        "PFTs (recent): FEV1 55% predicted. FEV1/FVC 0.62. DLCO 60% predicted.",
        "",
        "ASSESSMENT AND PLAN:",
        "1. COPD: GOLD Stage II, Group B. Continue tiotropium and PRN albuterol.",
        "2. Add ICS/LABA combination if exacerbation frequency increases.",
        "3. Smoking cessation: Patient quit 2 years ago. Reinforced counseling.",
        "4. Annual flu vaccine given today. Pneumovax up to date.",
        "5. Pulmonary rehab referral discussed. Follow up in 3 months."]),
    ("NEUROLOGY FOLLOW-UP", [
        "NEUROLOGY CLINIC NOTE",
        "",
        "SUBJECTIVE: Patient presents for TBI follow-up.",
        "Reports persistent headaches 3-4 days per week, mild-moderate intensity.",
        "Some difficulty with concentration and short-term memory.",
        "Dizziness improved with vestibular therapy.",
        "Sleep disrupted by nightmares. Currently in MH treatment for PTSD.",
        "",
        "OBJECTIVE:",
        "Neuro exam: Alert and oriented x4. Cranial nerves II-XII intact.",
        "Motor: 5/5 strength all extremities.",
        "Sensory: Intact to light touch, pinprick, vibration.",
        "Coordination: Finger-to-nose and heel-to-shin normal.",
        "Gait: Normal. Romberg negative.",
        "Montreal Cognitive Assessment (MoCA): 25/30.",
        "",
        "ASSESSMENT AND PLAN:",
        "1. Post-concussive syndrome: Persistent but slowly improving.",
        "2. Continue headache prophylaxis with topiramate.",
        "3. Continue vestibular rehabilitation.",
        "4. Neuropsych testing scheduled for detailed cognitive assessment.",
        "5. Follow up in 3 months."]),
    ("RHEUMATOLOGY FOLLOW-UP", [
        "RHEUMATOLOGY CLINIC NOTE",
        "",
        "SUBJECTIVE: Patient presents for rheumatoid arthritis follow-up.",
        "Joint stiffness in the morning lasting 30 minutes, improved from 90 minutes.",
        "Bilateral hand swelling has decreased. Able to open jars again.",
        "Fatigue moderate. No new joint involvement.",
        "Tolerating methotrexate well. Takes folic acid as prescribed.",
        "",
        "OBJECTIVE:",
        "Joints: Mild synovitis bilateral MCPs 2-3. No PIP swelling today.",
        "Wrists: Full ROM. No effusion.",
        "Knees: No effusion. Full ROM.",
        "Grip strength: 20 kg bilateral (improved from 15 kg).",
        "",
        "Labs: ESR 28 (prior 45). CRP 1.2 (prior 3.8). CBC normal. LFTs normal.",
        "",
        "ASSESSMENT AND PLAN:",
        "1. RA: Disease activity improving on current DMARD regimen. DAS28 2.8.",
        "2. Continue methotrexate 15mg weekly + hydroxychloroquine 200mg BID.",
        "3. Continue folic acid 1mg daily.",
        "4. Repeat labs (CBC, CMP, ESR, CRP) in 3 months.",
        "5. Follow up in 3 months. Consider biologic if plateau."]),
]

# Profile-specific note templates — more detailed, condition-specific SOAP notes
PROFILE_NOTE_TEMPLATES = {
    "TIBIAL_FRACTURE": [
        ("ORTHOPEDIC CLINIC NOTE", [
            "ORTHOPEDIC POST-OPERATIVE FOLLOW-UP",
            "",
            "SUBJECTIVE: Patient presents 6 weeks status post ORIF left tibial shaft fracture.",
            "Pain well controlled with oral medications. Using walker for ambulation.",
            "Has been non-weight-bearing per instructions. No fever or wound drainage.",
            "Numbness/tingling in left foot has improved since surgery.",
            "",
            "OBJECTIVE:",
            "Left lower extremity: Incision well-healed. No erythema or drainage.",
            "Mild residual swelling. Compartments soft.",
            "Ankle: ROM 10 degrees dorsiflexion, 30 degrees plantarflexion.",
            "Knee: ROM 0-120 degrees. Quad strength 4-/5.",
            "Sensation intact to light touch L4-S1 dermatomes.",
            "Pedal pulses 2+ bilaterally.",
            "",
            "X-rays: Progressive callus formation across fracture site.",
            "Hardware intact. Alignment maintained.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. Left tibial shaft fracture: Healing well. Callus formation noted.",
            "2. Advance to toe-touch weight-bearing in CAM boot.",
            "3. Begin physical therapy for ROM, strengthening, and gait training.",
            "4. PT referral placed for 2-3x/week x 8 weeks.",
            "5. Wean pain medications. Discontinue opioids by next visit.",
            "6. Follow up in 4 weeks with repeat x-rays."]),
        ("PT INITIAL EVALUATION", [
            "PHYSICAL THERAPY INITIAL EVALUATION",
            "",
            "DIAGNOSIS: S/P ORIF left tibial shaft fracture, 6 weeks post-op.",
            "REFERRAL: Orthopedic surgery. Authorization: 16 visits.",
            "",
            "SUBJECTIVE: Patient reports left leg pain at 4/10 with ambulation.",
            "Currently using front-wheeled walker. Non-weight-bearing until today.",
            "Goals: Return to independent ambulation without assistive device.",
            "Prior level of function: Independent community ambulator.",
            "",
            "OBJECTIVE:",
            "Left ankle ROM: DF 5 deg (N=20), PF 25 deg (N=50), Inv 15 deg (N=35), Ev 8 deg (N=15).",
            "Left knee ROM: Flex 110 deg (N=140), Ext 0 deg (N=0).",
            "Left hip ROM: WNL all planes.",
            "MMT: L quads 3+/5, L hamstrings 4-/5, L gastroc 3/5, L anterior tibialis 3+/5.",
            "Gait: TTWB with FWW. Trendelenburg noted on left. Step-to pattern.",
            "Balance: Modified Romberg positive. Single-leg stance unable on left.",
            "Circumferential measurements: L calf 2.5cm less than R calf.",
            "",
            "ASSESSMENT:",
            "Patient presents with significant deficits in ROM, strength, and",
            "function of the left lower extremity following tibial fracture and ORIF.",
            "Rehabilitation potential is good given age, motivation, and prior function level.",
            "",
            "PLAN:",
            "1. Progressive weight-bearing advancement per orthopedic protocol.",
            "2. Ankle and knee ROM exercises — AROM/AAROM progressing to stretching.",
            "3. LE strengthening: quad sets, SLR, hip abduction, calf raises (when WB allows).",
            "4. Gait training: progress FWW to cane to independent.",
            "5. Balance and proprioception training.",
            "6. Scar mobilization.",
            "7. Frequency: 2-3x/week for 8 weeks.",
            "8. Goals: Full ankle ROM, 4+/5 LE strength, independent ambulation by 12 weeks post-op."]),
        ("PT PROGRESS NOTE", [
            "PHYSICAL THERAPY PROGRESS NOTE",
            "",
            "DIAGNOSIS: S/P ORIF left tibial shaft fracture.",
            "VISIT: 8 of 16 authorized. 10 weeks post-op.",
            "",
            "SUBJECTIVE: Patient reports improving confidence with walking.",
            "Pain decreased to 2/10 with ambulation. Sleeping better.",
            "Practicing home exercises daily as prescribed.",
            "",
            "OBJECTIVE:",
            "Left ankle ROM: DF 12 deg (was 5), PF 38 deg (was 25).",
            "Left knee ROM: Flex 130 deg (was 110). Full extension.",
            "MMT: L quads 4/5 (was 3+), L gastroc 3+/5 (was 3).",
            "Gait: PWB 50% with single-point cane. Improved step-through pattern.",
            "Balance: Single-leg stance L 8 seconds (was unable).",
            "Timed Up and Go: 18 seconds (was 32 seconds).",
            "",
            "TREATMENT PROVIDED:",
            "Manual therapy: ankle joint mobilization grades III-IV.",
            "Therapeutic exercise: mini-squats, step-ups, calf raises bilateral.",
            "Gait training with cane on varied surfaces.",
            "Balance training: tandem stance, single-leg activities.",
            "",
            "ASSESSMENT: Good progress. ROM and strength improving steadily.",
            "Patient motivated and compliant with HEP.",
            "",
            "PLAN: Continue 2x/week. Progress to full weight-bearing.",
            "Begin proprioceptive training and sport-specific drills if appropriate.",
            "Estimated 6-8 more visits to achieve discharge goals."]),
    ],
    "HIP_FRACTURE": [
        ("ORTHOPEDIC CLINIC NOTE", [
            "ORTHOPEDIC POST-OPERATIVE FOLLOW-UP",
            "",
            "SUBJECTIVE: Patient presents 2 weeks status post left hip hemiarthroplasty",
            "for displaced femoral neck fracture.",
            "Pain controlled with Tylenol and low-dose opioid.",
            "Ambulating with rolling walker. Following posterior hip precautions.",
            "Home health PT has been visiting 3x/week.",
            "",
            "OBJECTIVE:",
            "Left hip: Incision clean, dry, intact. Staples in place (to remove today).",
            "No erythema or drainage. No calf tenderness.",
            "ROM (passive): Flexion 80 degrees, abduction 25 degrees.",
            "Able to perform SLR against gravity.",
            "",
            "X-ray: Prosthesis in good position. No periprosthetic fracture.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. S/P left hip hemiarthroplasty: Progressing well.",
            "2. Remove staples today. Steri-strips applied.",
            "3. Continue weight-bearing as tolerated with walker.",
            "4. Continue posterior hip precautions x6 weeks.",
            "5. Transition to outpatient PT for continued strengthening and gait training.",
            "6. Wean opioid. Increase Tylenol for pain management.",
            "7. Follow up in 4 weeks."]),
        ("PT INITIAL EVALUATION", [
            "PHYSICAL THERAPY INITIAL EVALUATION",
            "",
            "DIAGNOSIS: S/P left hip hemiarthroplasty for femoral neck fracture.",
            "REFERRAL: Orthopedic surgery. Authorization: 12 visits.",
            "PRECAUTIONS: Posterior hip precautions x6 weeks.",
            "",
            "SUBJECTIVE: Patient is an elderly veteran who sustained a fall resulting in",
            "left femoral neck fracture. S/P hemiarthroplasty 3 weeks ago.",
            "Currently using rolling walker. Pain 5/10 with ambulation.",
            "Lives alone. Prior level: independent with cane for community ambulation.",
            "Fall risk assessment: High (Morse Fall Scale 65).",
            "",
            "OBJECTIVE:",
            "Left hip ROM (within precautions): Flex 75 deg, Abd 20 deg, ER 15 deg.",
            "Left knee ROM: 0-125 degrees. Left ankle: WNL.",
            "MMT: L hip flex 3/5, L hip abd 3-/5, L quads 3+/5, L glut max 3/5.",
            "Gait: WBAT with rolling walker. Short stride length. Antalgic on left.",
            "Transfers: Mod assist for sit-to-stand. Independent bed mobility.",
            "Balance: Berg Balance Scale 32/56 (fall risk).",
            "",
            "ASSESSMENT: Significant impairments in LE strength, balance, and",
            "functional mobility. Fall risk remains elevated. Patient is motivated.",
            "",
            "PLAN:",
            "1. LE strengthening within hip precautions.",
            "2. Balance and fall prevention training.",
            "3. Gait training: progress walker to cane.",
            "4. Transfer training for home safety.",
            "5. Home exercise program for daily performance.",
            "6. Frequency: 2x/week for 6 weeks."]),
    ],
    "ANKLE_FRACTURE": [
        ("ORTHOPEDIC CLINIC NOTE", [
            "ORTHOPEDIC POST-OPERATIVE FOLLOW-UP",
            "",
            "SUBJECTIVE: 4 weeks status post ORIF left bimalleolar ankle fracture.",
            "Pain improving. Using crutches, non-weight-bearing on left.",
            "Swelling has decreased. No wound concerns.",
            "",
            "OBJECTIVE:",
            "Left ankle: Incision healed. Hardware palpable. No tenderness over hardware.",
            "Swelling 1+ compared to right. No ecchymosis.",
            "ROM: DF -5 degrees, PF 20 degrees. Inv/Ev guarded.",
            "",
            "X-rays: Fracture lines healing. Hardware intact. Mortise congruent.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. Left ankle bimalleolar fracture: Healing on schedule.",
            "2. Transition to CAM boot. Begin partial weight-bearing 25% BW.",
            "3. Start physical therapy for ROM and progressive weight-bearing.",
            "4. Follow up in 4 weeks with weight-bearing x-rays."]),
    ],
    "ACL_TEAR": [
        ("ORTHOPEDIC CLINIC NOTE", [
            "ORTHOPEDIC POST-OPERATIVE FOLLOW-UP",
            "",
            "SUBJECTIVE: 2 weeks status post right ACL reconstruction with BTB autograft.",
            "Tolerating brace well. Minimal pain with medication.",
            "Ice and elevation as directed. No fever or wound concerns.",
            "",
            "OBJECTIVE:",
            "Right knee: Incisions clean. Mild effusion. No warmth.",
            "ROM: 0-90 degrees in brace (limited per protocol).",
            "Quad firing present. Straight-leg raise possible with brace.",
            "Lachman: Stable endpoint.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. R ACL reconstruction: Progressing per protocol.",
            "2. Unlock brace for ROM exercises. Goal: full extension, 120 degrees flexion by 6 weeks.",
            "3. Begin outpatient PT — ACL reconstruction protocol.",
            "4. Weight-bearing as tolerated in locked brace.",
            "5. Follow up in 4 weeks."]),
        ("PT INITIAL EVALUATION", [
            "PHYSICAL THERAPY INITIAL EVALUATION — ACL RECONSTRUCTION PROTOCOL",
            "",
            "DIAGNOSIS: S/P right ACL reconstruction with BTB autograft, 2 weeks post-op.",
            "REFERRAL: Orthopedic surgery. Authorization: 24 visits.",
            "PROTOCOL: Progressive ACL rehabilitation, 6-9 month return to activity.",
            "",
            "SUBJECTIVE: Young veteran sustained ACL tear during recreational sports.",
            "S/P arthroscopic ACL reconstruction with BTB autograft 2 weeks ago.",
            "Pain 3/10 at rest, 5/10 with exercise. Sleeping in brace as instructed.",
            "Goal: Return to full activity including running and sports.",
            "",
            "OBJECTIVE:",
            "Right knee ROM: Extension 0 degrees (full), Flexion 85 degrees.",
            "Effusion: 2+ (moderate). Patellar mobility: restricted superiorly.",
            "VMO activation present but weak. SLR possible.",
            "MMT: R quads 3/5, R hamstrings 4-/5.",
            "Gait: WBAT in locked hinged brace. Antalgic on right.",
            "Donor site: Patellar tendon harvest site tender to palpation.",
            "",
            "ASSESSMENT: Patient in Phase 1 of ACL rehab protocol.",
            "Primary deficits: knee flexion ROM, quad strength, effusion control.",
            "",
            "PLAN — PHASE 1 (Weeks 0-6):",
            "1. Restore full extension, progress flexion to 120 degrees.",
            "2. Quad activation: e-stim, quad sets, SLR in 4 planes.",
            "3. Patellar mobilization. Scar mobilization at 4 weeks.",
            "4. Effusion management: ice, compression, elevation.",
            "5. Begin closed-chain exercises: mini-squats, leg press (limited range).",
            "6. Gait training: normalize pattern, wean from brace.",
            "7. Frequency: 3x/week for 6 weeks, then reassess."]),
    ],
    "COMPLEX_DIABETIC": [
        ("ENDOCRINOLOGY CLINIC NOTE", [
            "ENDOCRINOLOGY COMPREHENSIVE DIABETES MANAGEMENT",
            "",
            "SUBJECTIVE: Patient presents for comprehensive diabetes review.",
            "Reports numbness and burning in both feet, worse at night.",
            "Home glucose log: fasting 130-200, post-prandial 180-280.",
            "Had 2 hypoglycemic episodes (glucose <70) last month while fasting.",
            "Following diabetic diet inconsistently. Sedentary lifestyle.",
            "Last eye exam 3 months ago showed mild non-proliferative diabetic retinopathy.",
            "",
            "OBJECTIVE:",
            "Foot exam: Monofilament absent at 3/10 sites bilaterally.",
            "Vibration sense decreased bilateral great toes.",
            "No ulcers. Skin dry with some cracking. Nails thickened.",
            "Ankle reflexes absent bilaterally.",
            "",
            "Labs: HbA1c 9.1% (prior 8.5%). Creatinine 1.8. eGFR 42.",
            "Microalbumin/Creatinine ratio: 180 mg/g (elevated).",
            "Fasting glucose 198. LDL 128. TG 245.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. T2DM: Poorly controlled. A1C 9.1%. Add semaglutide 0.25mg SQ weekly.",
            "2. Diabetic neuropathy: Worsening. Increase gabapentin to 600mg TID.",
            "3. CKD Stage 3b (eGFR 42): Declining from 55 last year. Refer nephrology.",
            "4. Proteinuria: Maximize ACE inhibitor dose. Consider adding finerenone.",
            "5. Diabetic retinopathy: Ophthalmology follow-up in 6 months.",
            "6. Hyperlipidemia: LDL above goal. Increase atorvastatin to 40mg.",
            "7. Podiatry referral for comprehensive foot care.",
            "8. Nutrition consult for carb counting and meal planning.",
            "9. Recheck A1C, BMP, lipids, microalbumin in 3 months."]),
    ],
    "POLYTRAUMA": [
        ("POLYTRAUMA CLINIC NOTE", [
            "POLYTRAUMA/TBI CLINIC NOTE",
            "",
            "SUBJECTIVE: Patient presents for polytrauma follow-up.",
            "Reports ongoing headaches, 5-6 days per week, intensity 5-7/10.",
            "Persistent tinnitus bilateral, constant. Hearing aids in use.",
            "Chronic low back pain, worse with prolonged sitting. Rating 6/10.",
            "Sleep disrupted by nightmares 3-4 nights per week.",
            "Hypervigilance in crowds. Avoids loud noises.",
            "Memory and concentration difficulties affecting daily activities.",
            "",
            "OBJECTIVE:",
            "Neurological: Oriented x4. Cranial nerves intact.",
            "MoCA: 24/30 (deficits in delayed recall and executive function).",
            "Audiometry: Bilateral sensorineural hearing loss, moderate severity.",
            "Tinnitus Handicap Inventory: 52/100 (moderate handicap).",
            "Lumbar spine: Paravertebral tenderness. ROM limited by 30%.",
            "Straight leg raise negative bilaterally.",
            "",
            "PCL-5: 48 (above threshold for probable PTSD).",
            "PHQ-9: 15 (moderately severe depression).",
            "",
            "ASSESSMENT AND PLAN:",
            "1. TBI: Persistent post-concussive symptoms. Continue topiramate for headache.",
            "2. Hearing loss: Hearing aids functioning well. Follow-up audiology in 6 months.",
            "3. Tinnitus: Trial of sound masking therapy. Tinnitus retraining referral.",
            "4. PTSD: Continue sertraline 100mg and prazosin 2mg QHS. Therapy ongoing.",
            "5. Chronic pain: Continue multimodal approach. PT referral for spine program.",
            "6. Neuropsychology testing recommended for cognitive rehabilitation planning.",
            "7. Interdisciplinary team conference scheduled.",
            "8. Follow up in 2 months."]),
    ],
    "CARDIAC_COMPLEX": [
        ("CARDIOLOGY CLINIC NOTE", [
            "CARDIOLOGY COMPREHENSIVE HEART FAILURE MANAGEMENT",
            "",
            "SUBJECTIVE: Patient presents for CHF management.",
            "Reports increasing DOE over the last 2 weeks — now symptomatic climbing 1 flight.",
            "Orthopnea: Using 3 pillows to sleep. Wakes once nightly with SOB.",
            "Weight has increased 4 lbs in the last week despite diuretic use.",
            "Denies chest pain or palpitations. Reports occasional skipped beats.",
            "Diet: Admits to dietary indiscretion (Chinese food last weekend).",
            "",
            "OBJECTIVE:",
            "JVP: Elevated to 10 cm H2O.",
            "Cardiovascular: Irregularly irregular, rate 88. S3 gallop present.",
            "Grade 2/6 systolic murmur at apex radiating to axilla.",
            "Lungs: Bibasilar crackles to mid-lung fields bilaterally.",
            "Abdomen: Hepatojugular reflux positive.",
            "Extremities: 2+ pitting edema bilateral lower extremities to mid-calf.",
            "Weight: 198 lbs (was 194 lbs 2 weeks ago).",
            "",
            "Labs: BNP 580 pg/mL (prior 185). Na 134. K 4.8. Cr 1.6. INR 2.1.",
            "Echo (1 month ago): LVEF 35% (was 40% 6 months ago).",
            "",
            "ASSESSMENT AND PLAN:",
            "1. CHF exacerbation: NYHA Class III (worsening from II).",
            "   - Increase furosemide to 80mg BID from 40mg BID.",
            "   - Add metolazone 2.5mg daily x3 days for diuretic resistance.",
            "   - Daily weights. Call if weight increases >2 lbs in 24 hours.",
            "2. Atrial fibrillation: Rate borderline. Increase metoprolol to 50mg BID.",
            "3. Declining EF: Consider addition of sacubitril/valsartan if tolerated.",
            "4. Sodium restriction: <2g/day. Fluid restriction <1.5L/day.",
            "5. Dietary counseling reinforced. Nutrition referral placed.",
            "6. Reassess in 1 week. If no improvement, consider IV diuresis admission.",
            "7. Repeat echo in 3 months. BNP in 1 week."]),
    ],
    "BKA": [
        ("SURGERY POST-OP NOTE", [
            "SURGICAL FOLLOW-UP — BELOW-KNEE AMPUTATION",
            "",
            "SUBJECTIVE: Patient presents 3 weeks status post left transtibial amputation.",
            "Residual limb pain 5/10, improving. Reports phantom limb sensations.",
            "Phantom pain 4/10 in left foot (burning, tingling). Worse at night.",
            "Adjusting to wheelchair mobility. Emotional status: some grief reaction.",
            "Attending psychology sessions as recommended.",
            "",
            "OBJECTIVE:",
            "Residual limb: Incision healing well. Sutures intact. No infection.",
            "Moderate edema. Shaping with elastic bandage wrapping.",
            "No skin breakdown. Stump length adequate for prosthetic fitting.",
            "Upper extremity strength 5/5 bilateral. Core strength 4/5.",
            "Independent with wheelchair transfers.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. Left BKA: Healing progressing. Continue wound care and edema management.",
            "2. Phantom limb pain: Start gabapentin 300mg TID. Mirror therapy initiated.",
            "3. Prosthetics consultation placed for prosthetic limb fitting timeline.",
            "4. Continue PT for pre-prosthetic training, transfers, and upper body conditioning.",
            "5. Psychology for adjustment disorder management.",
            "6. Follow up in 2 weeks for suture removal and prosthetic planning."]),
    ],
    "BILATERAL_KNEE_OA": [
        ("ORTHOPEDIC CLINIC NOTE", [
            "ORTHOPEDIC PRE-OPERATIVE EVALUATION — TOTAL KNEE ARTHROPLASTY",
            "",
            "SUBJECTIVE: Patient presents for pre-op evaluation for left TKA.",
            "Bilateral knee pain for 8 years, progressively worsening.",
            "Left knee more symptomatic: pain 7/10 with walking, 4/10 at rest.",
            "Failed conservative management: PT, NSAIDs, cortisone injections (x3),",
            "hyaluronic acid injections, bracing, and activity modification.",
            "Walking limited to 1 block. Difficulty with stairs. Using cane.",
            "Right knee planned for future surgery.",
            "",
            "OBJECTIVE:",
            "Left knee: Varus deformity 8 degrees. Crepitus with ROM.",
            "ROM: 5-105 degrees (10-degree flexion contracture).",
            "Medial joint line tenderness. Small effusion.",
            "Ligaments: Stable to varus/valgus stress. ACL/PCL intact.",
            "Right knee: Similar findings, less symptomatic.",
            "",
            "X-rays: Severe medial compartment OA bilaterally, bone-on-bone.",
            "Large osteophytes. Subchondral sclerosis and cysts.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. Severe bilateral knee OA: Surgical candidate for left TKA.",
            "2. Pre-operative clearance obtained — cardiac and anesthesia.",
            "3. Surgery scheduled. Pre-op PT (prehab) for quad strengthening.",
            "4. Discussed risks, benefits, alternatives. Consent obtained.",
            "5. Post-op plan: Inpatient rehab vs home PT based on progress.",
            "6. Right TKA planned 4-6 months after left recovery."]),
    ],
    "SPINE_COMPLEX": [
        ("SPINE CLINIC NOTE", [
            "SPINE CLINIC EVALUATION",
            "",
            "SUBJECTIVE: Patient presents with worsening low back pain radiating to left leg.",
            "Pain rated 7/10, described as sharp and burning down posterior left thigh to calf.",
            "Onset: Acute worsening 3 weeks ago while lifting.",
            "Aggravating factors: Sitting >20 min, bending, lifting.",
            "Relieving factors: Standing, walking short distances, lying flat.",
            "Denies bowel or bladder dysfunction. No saddle anesthesia.",
            "Has tried ibuprofen, acetaminophen, and ice without adequate relief.",
            "",
            "OBJECTIVE:",
            "Lumbar: Paravertebral muscle spasm bilateral. Decreased lordosis.",
            "ROM: Flexion limited to 30 degrees (pain), Extension 10 degrees.",
            "Lateral flexion limited bilateral. Rotation limited by pain.",
            "SLR: Positive on left at 35 degrees reproducing radicular symptoms.",
            "Neuro: L5 weakness (EHL 4/5 left). Sensation decreased L5 dermatome left.",
            "Ankle reflex present bilateral. Knee reflex 2+ bilateral.",
            "Babinski negative bilaterally.",
            "",
            "MRI: L4-5 left paracentral disc extrusion compressing left L5 nerve root.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. L4-5 disc herniation with left L5 radiculopathy.",
            "2. Start oral steroid taper: Medrol dose pack.",
            "3. Gabapentin 300mg TID for neuropathic pain.",
            "4. Physical therapy referral: McKenzie-based approach, core stabilization.",
            "5. Epidural steroid injection if no improvement in 4-6 weeks.",
            "6. Surgical consultation only if fail conservative management x 6-12 weeks.",
            "7. Activity modification: avoid heavy lifting. Ergonomic education.",
            "8. Follow up in 4 weeks."]),
    ],
    "STROKE_REHAB": [
        ("REHABILITATION MEDICINE NOTE", [
            "INPATIENT REHABILITATION MEDICINE NOTE",
            "",
            "SUBJECTIVE: Day 5 of inpatient rehabilitation following acute right MCA CVA.",
            "Patient alert but fatigued. Left-sided weakness improving slowly.",
            "Able to follow 2-step commands consistently. Speech slightly dysarthric.",
            "Motivation fair. Reports frustration with left arm weakness.",
            "Family supportive. Spouse present during therapy sessions.",
            "",
            "OBJECTIVE:",
            "Neuro: Alert, oriented x3 (date confused). Left neglect resolving.",
            "Motor: Right UE/LE 5/5. Left UE 2/5 prox, 1/5 distal. Left LE 3/5.",
            "Sensory: Decreased light touch left UE and LE.",
            "Speech: Mild dysarthria. Comprehension intact.",
            "Swallowing: Cleared by SLP for mechanical soft diet.",
            "FIM scores: Self-care 3 (mod assist), Mobility 3, Cognition 5.",
            "",
            "THERAPY PROGRESS:",
            "PT: Standing with mod assist. Beginning parallel bar gait training.",
            "OT: ADL training with adaptive equipment. Left UE AROM exercises.",
            "SLP: Articulation exercises. Cognitive-linguistic therapy.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. R MCA stroke with L hemiparesis: Making functional gains.",
            "2. Continue intensive inpatient rehab (PT/OT/SLP 3 hrs/day).",
            "3. DVT prophylaxis: Continue enoxaparin.",
            "4. Start aspirin 81mg + atorvastatin 80mg for secondary prevention.",
            "5. BP management: Target <130/80.",
            "6. Depression screening in 1 week (common post-stroke).",
            "7. Family meeting scheduled to discuss discharge planning.",
            "8. Projected length of stay: 2-3 more weeks."]),
    ],
    "MH_COMPLEX": [
        ("MENTAL HEALTH COMPREHENSIVE NOTE", [
            "MENTAL HEALTH COMPREHENSIVE EVALUATION",
            "",
            "SUBJECTIVE: Patient presents for mental health follow-up.",
            "Reports persistent nightmares about combat experiences 4-5 nights/week.",
            "Hypervigilance: Cannot sit with back to door. Startles easily.",
            "Mood: Depressed. Rating 3/10 (10=best). Anhedonia prominent.",
            "Sleep: 3-4 hours/night despite trazodone. Frequent awakenings.",
            "Appetite: Poor. Lost 8 lbs in last 2 months.",
            "Substance use: Reports drinking 4-6 beers daily to 'numb out.'",
            "Admits this has increased from 2-3 beers at last visit.",
            "Denies suicidal ideation but reports passive death wish ('wouldn't mind not waking up').",
            "No plan or intent. Firearms in home — stored at friend's house per safety plan.",
            "",
            "OBJECTIVE:",
            "Appearance: Unkempt. Lost weight since last visit.",
            "Psychomotor: Slightly retarded. Long response latency.",
            "Speech: Low volume. Slow rate.",
            "Mood: 'Terrible.' Affect: Constricted, tearful at times.",
            "Thought process: Linear but slowed.",
            "Thought content: Passive death wish. No active SI. No HI.",
            "Cognition: Oriented x4. Concentration impaired.",
            "",
            "PHQ-9: 22 (severe depression). GAD-7: 18 (severe anxiety).",
            "PCL-5: 62 (severe PTSD). AUDIT-C: 8 (harmful use).",
            "Columbia Suicide Severity: Category 2 (non-specific active SI).",
            "",
            "ASSESSMENT AND PLAN:",
            "1. PTSD: Severe. Increase prazosin to 5mg QHS for nightmares.",
            "2. MDD: Severe. Cross-taper sertraline to venlafaxine XR 75mg.",
            "3. Alcohol use disorder: Worsening. Refer to SATP (substance abuse treatment).",
            "4. Safety: Firearms removed. Safety plan updated. Crisis line reviewed.",
            "5. Increase therapy to 2x/week. CPT (Cognitive Processing Therapy) sessions.",
            "6. Nutritional support: Ensure supplements. Social work consult for benefits.",
            "7. Close follow-up in 1 week given severity and passive SI.",
            "8. If worsening, consider inpatient psychiatric admission."]),
    ],
    "WRIST_FRACTURE": [
        ("ORTHOPEDIC CLINIC NOTE", [
            "ORTHOPEDIC POST-OPERATIVE FOLLOW-UP",
            "",
            "SUBJECTIVE: 2 weeks status post ORIF right distal radius fracture.",
            "Splint removed today. Pain 3/10. Fingers moving well.",
            "Numbness in thumb and index finger has resolved since surgery.",
            "",
            "OBJECTIVE:",
            "Right wrist: Incision clean, healing well. Mild swelling.",
            "Finger ROM: Full flexion and extension all digits. Grip weak.",
            "Wrist ROM: DF 15 degrees, PF 20 degrees, Rad Dev 5 degrees.",
            "",
            "X-rays: Hardware intact. Articular surface maintained. No displacement.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. R distal radius fracture s/p ORIF: Healing appropriately.",
            "2. Begin gentle active ROM exercises for wrist.",
            "3. Hand therapy/PT referral for ROM, grip strength, scar management.",
            "4. Removable wrist splint for comfort, remove for exercises.",
            "5. Follow up in 4 weeks with repeat x-rays."]),
    ],
    "PULMONARY_COMPLEX": [
        ("PULMONOLOGY CLINIC NOTE", [
            "PULMONOLOGY COMPREHENSIVE EVALUATION",
            "",
            "SUBJECTIVE: Patient presents for comprehensive pulmonary follow-up.",
            "COPD: Stable with 1 exacerbation in last 6 months. Currently at baseline.",
            "Uses 2L O2 at night per sleep study. CPAP compliance 5.2 hrs/night.",
            "Dyspnea: mMRC grade 2 (SOB on hurrying or walking up slight hill).",
            "CHF: Lower extremity edema managed with diuretics.",
            "Depression: Affecting motivation for pulmonary rehab attendance.",
            "",
            "OBJECTIVE:",
            "SpO2: 92% on room air at rest, 88% with 6-minute walk.",
            "6-minute walk distance: 280 meters (below predicted).",
            "Lungs: Scattered expiratory wheezes. Prolonged expiratory phase.",
            "",
            "PFTs: FEV1 48% predicted (was 55% 1 year ago). FEV1/FVC 0.58.",
            "DLCO 52% predicted.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. COPD: GOLD Stage III (progression). Add roflumilast to reduce exacerbations.",
            "2. Pulmonary rehab: Re-referral with coordination with MH team for motivation.",
            "3. OSA: CPAP compliance acceptable. Continue current settings.",
            "4. Supplemental O2: Qualify for exertional O2. Prescribe 2L NC with activity.",
            "5. CHF: Coordinate with cardiology for diuretic management.",
            "6. Flu and pneumonia vaccines current.",
            "7. Annual LDCT for lung cancer screening due in 2 months.",
            "8. Follow up in 3 months with repeat PFTs."]),
    ],
    "ROTATOR_CUFF": [
        ("ORTHOPEDIC CLINIC NOTE", [
            "ORTHOPEDIC POST-OPERATIVE FOLLOW-UP",
            "",
            "SUBJECTIVE: 6 weeks status post arthroscopic left rotator cuff repair.",
            "Wearing sling as instructed. Pain 3/10, well-controlled.",
            "Home passive ROM exercises have been going well.",
            "Spouse assists with pendulum exercises and passive elevation.",
            "",
            "OBJECTIVE:",
            "Left shoulder: Well-healed portal sites. No infection.",
            "Passive ROM: Forward elevation 130 degrees. ER at side 30 degrees.",
            "No active ROM testing at this time per protocol.",
            "Deltoid and periscapular muscles firing with isometrics.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. L rotator cuff repair: Healing well per protocol.",
            "2. Discontinue sling. Begin active-assisted ROM.",
            "3. Physical therapy to begin — rotator cuff repair protocol.",
            "4. No lifting >1 lb with left arm until 12 weeks post-op.",
            "5. Follow up in 6 weeks."]),
        ("PT INITIAL EVALUATION", [
            "PHYSICAL THERAPY INITIAL EVALUATION — ROTATOR CUFF REPAIR PROTOCOL",
            "",
            "DIAGNOSIS: S/P arthroscopic left rotator cuff repair, 6 weeks post-op.",
            "REFERRAL: Orthopedic surgery. Authorization: 20 visits.",
            "PROTOCOL: Phase 2 — Active-assisted to active ROM.",
            "",
            "SUBJECTIVE: Patient reports improving pain. Sling discontinued today.",
            "Dominant hand: Right. Left shoulder is non-dominant.",
            "Goal: Return to overhead activities and yard work.",
            "",
            "OBJECTIVE:",
            "L shoulder PROM: Flexion 135 deg, Abduction 90 deg, ER at 0 deg abd 35 deg.",
            "IR at 90 abd: 30 degrees. Horizontal adduction: 90 degrees.",
            "Active ROM: Flexion 80 degrees (with hiking). Abduction 60 degrees.",
            "Scapular control: Poor. Scapular winging with attempted elevation.",
            "Grip strength: Left 15 kg (Right 35 kg).",
            "",
            "ASSESSMENT: Significant ROM and strength deficits expected at this stage.",
            "Patient progressing appropriately per surgical protocol timeline.",
            "",
            "PLAN — PHASE 2 (Weeks 6-12):",
            "1. Active-assisted ROM: pulleys, wand exercises, wall slides.",
            "2. Scapular stabilization exercises: rows, serratus punches.",
            "3. Gentle isometrics: ER/IR at side.",
            "4. Scar mobilization and soft tissue mobilization.",
            "5. NO resisted ER/IR or overhead strengthening until 12 weeks.",
            "6. Frequency: 2x/week for 6 weeks, then reassess for Phase 3."]),
    ],
    "ACHILLES_RUPTURE": [
        ("ORTHOPEDIC CLINIC NOTE", [
            "ORTHOPEDIC POST-OPERATIVE FOLLOW-UP",
            "",
            "SUBJECTIVE: 6 weeks status post right Achilles tendon repair.",
            "Transitioned from posterior splint to CAM boot 2 weeks ago.",
            "Weight-bearing as tolerated in boot. Pain minimal.",
            "",
            "OBJECTIVE:",
            "Right ankle: Incision well healed. Palpable repair intact.",
            "Ankle ROM (in boot): DF to neutral, PF 30 degrees.",
            "Thompson test: Positive (expected at this stage, tendon not yet strong).",
            "Calf atrophy noted compared to left.",
            "",
            "ASSESSMENT AND PLAN:",
            "1. R Achilles repair: Healing on schedule.",
            "2. Begin gentle active ROM outside of boot.",
            "3. PT referral for Achilles repair protocol.",
            "4. Continue CAM boot for walking. Remove for exercises only.",
            "5. No running or jumping for 4-6 months.",
            "6. Follow up in 6 weeks."]),
    ],
}

ADT_WARDS = ["ICU","TELEMETRY","SURGERY","NEUROLOGY","MEDICINE","STEP-DOWN"]
ADT_ROOMS = {
    "ICU": ["ICU-1A","ICU-2A","ICU-3A","ICU-4A","ICU-5B","ICU-6B"],
    "TELEMETRY": ["T101A","T102B","T205B","T301A","T108A","T210A"],
    "SURGERY": ["S101A","S102B","S204A","S303B","S105A","S206B"],
    "NEUROLOGY": ["N201A","N202B","N301A","N302B"],
    "MEDICINE": ["M101A","M102B","M201A","M202B","M301A","M302B"],
    "STEP-DOWN": ["SD101","SD102","SD201","SD202"],
}

# ── New data pools for expanded ZWR files ──────────────────────────────────

# Immunization pool: (name, CVX code)
IMMUNIZATION_POOL = [
    ("INFLUENZA", "158"),
    ("COVID-19 MRNA", "213"),
    ("TDAP", "115"),
    ("PNEUMOVAX 23", "33"),
    ("SHINGRIX", "187"),
    ("HEPATITIS B", "45"),
    ("HEPATITIS A", "83"),
    ("MMR", "03"),
]

IMMUNIZATION_SITES = ["LEFT DELTOID","RIGHT DELTOID","LEFT THIGH","RIGHT THIGH","LEFT GLUTEAL","RIGHT GLUTEAL"]
IMMUNIZATION_ROUTES = ["INTRAMUSCULAR","SUBCUTANEOUS"]
IMMUNIZATION_MANUFACTURERS = ["PFIZER","MODERNA","MERCK","GSK","SANOFI","SEQIRUS"]
IMMUNIZATION_SERIES = ["COMPLETE","1 OF 2","2 OF 2","1 OF 3","2 OF 3","3 OF 3","BOOSTER"]

# Dental procedure pool: (code, description)
DENTAL_PROCEDURES = [
    ("D0120","PERIODIC ORAL EXAM"),
    ("D0150","COMPREHENSIVE ORAL EXAM"),
    ("D0210","FULL MOUTH RADIOGRAPHS"),
    ("D1110","PROPHYLAXIS ADULT"),
    ("D2391","COMPOSITE FILLING"),
    ("D7140","EXTRACTION"),
    ("D2740","CROWN PORCELAIN"),
]

DENTAL_SURFACES = ["M","O","D","B","L","MO","DO","MOD","BOL","FULL"]

# Mental health instruments: (name, max_score)
MH_INSTRUMENTS = [
    ("PHQ-9", 27),
    ("GAD-7", 21),
    ("PCL-5", 80),
    ("AUDIT-C", 12),
    ("COLUMBIA SUICIDE SEVERITY", 6),
]

# Social work assessment types
SW_ASSESS_TYPES = ["PSYCHOSOCIAL","DISCHARGE_PLANNING","HOMELESS_SCREENING","CAREGIVER"]
SW_HOUSING = ["HOUSED","HOMELESS","AT-RISK","TRANSITIONAL","SHELTER"]
SW_EMPLOYMENT = ["EMPLOYED","UNEMPLOYED","RETIRED","DISABLED"]
SW_SUPPORT = ["STRONG","MODERATE","LIMITED","NONE"]
SW_SUBSTANCE = ["NONE","ALCOHOL","TOBACCO","CANNABIS","POLYSUBSTANCE","IN RECOVERY"]
SW_LEGAL = ["NONE","PENDING CHARGES","PROBATION","CHILD CUSTODY","VA BENEFITS APPEAL"]
SW_DISCHARGE_BARRIERS = [
    "LACK OF HOUSING","NO CAREGIVER","INSURANCE COVERAGE","TRANSPORTATION",
    "MEDICATION COSTS","MENTAL HEALTH NEEDS","SUBSTANCE ABUSE","COGNITIVE IMPAIRMENT",
]
SW_RECOMMENDATIONS = [
    "REFER TO HOUSING PROGRAM","CONNECT WITH PEER SUPPORT","BENEFITS APPLICATION ASSISTANCE",
    "HOME HEALTH REFERRAL","CAREGIVER RESPITE","SUBSTANCE ABUSE TREATMENT","LEGAL AID REFERRAL",
    "VOCATIONAL REHABILITATION","COMMUNITY RESOURCE LIAISON","MENTAL HEALTH INTENSIVE OUTPATIENT",
]

# Health factors
HEALTH_FACTOR_POOL = [
    ("CURRENT SMOKER","TOBACCO","POSITIVE"),
    ("FORMER SMOKER","TOBACCO","HISTORICAL"),
    ("ALCOHOL USE","SUBSTANCE","POSITIVE"),
    ("EXERCISE","LIFESTYLE","POSITIVE"),
    ("HOMELESS","SOCIAL","POSITIVE"),
    ("COMBAT VETERAN","MILITARY","POSITIVE"),
    ("MST SCREEN POSITIVE","SCREENING","POSITIVE"),
    ("FALL RISK","SAFETY","POSITIVE"),
    ("DEPRESSION SCREEN POSITIVE","SCREENING","POSITIVE"),
    ("OBESITY","HEALTH","POSITIVE"),
    ("CPAP USE","DEVICE","POSITIVE"),
    ("HEARING AID USE","DEVICE","POSITIVE"),
]

# Diet orders
DIET_POOL = [
    ("REGULAR","REGULAR DIET","","REGULAR","THIN","2000"),
    ("CARDIAC","CARDIAC DIET","LOW FAT;LOW CHOLESTEROL","REGULAR","THIN","1800"),
    ("DIABETIC","DIABETIC DIET","NO CONCENTRATED SWEETS;CARB CONTROLLED","REGULAR","THIN","1800"),
    ("RENAL","RENAL DIET","LOW SODIUM;LOW POTASSIUM;LOW PHOSPHORUS","REGULAR","THIN","2000"),
    ("LOW SODIUM","LOW SODIUM DIET","2GM SODIUM RESTRICTION","REGULAR","THIN","2000"),
    ("NPO","NPO","NOTHING BY MOUTH","NPO","NPO","0"),
    ("PUREED","PUREED DIET","","PUREED","NECTAR THICK","1800"),
    ("CLEAR LIQUID","CLEAR LIQUID DIET","","LIQUID","THIN","1200"),
]

# Prosthetics pool: (item, HCPCS, category)
PROSTHETICS_POOL = [
    ("HEARING AID DIGITAL","V5261","SENSORY AIDS"),
    ("WHEELCHAIR STANDARD","K0001","WHEELED MOBILITY"),
    ("CPAP DEVICE","E0601","RESPIRATORY"),
    ("KNEE BRACE HINGED","L1810","ORTHOTIC"),
    ("BACK BRACE LSO","L0631","ORTHOTIC"),
    ("PROSTHETIC LIMB BELOW KNEE","L5301","PROSTHETIC"),
    ("ADJUSTABLE CANE","E0100","AMBULATORY AIDS"),
    ("FOLDING WALKER","E0143","AMBULATORY AIDS"),
    ("SHOWER CHAIR","E0240","BATHROOM SAFETY"),
]

# Clinical reminder pool
REMINDER_POOL = [
    ("ANNUAL PHYSICAL","PREVENTIVE","ROUTINE","ANNUAL"),
    ("INFLUENZA VACCINE","IMMUNIZATION","ROUTINE","ANNUAL"),
    ("COLORECTAL SCREENING","CANCER SCREENING","ROUTINE","10 YEARS"),
    ("BREAST CANCER SCREENING","CANCER SCREENING","ROUTINE","2 YEARS"),
    ("CERVICAL CANCER SCREENING","CANCER SCREENING","ROUTINE","3 YEARS"),
    ("DIABETIC EYE EXAM","CHRONIC DISEASE","ROUTINE","ANNUAL"),
    ("DIABETIC FOOT EXAM","CHRONIC DISEASE","ROUTINE","ANNUAL"),
    ("LIPID PANEL","LAB MONITORING","ROUTINE","ANNUAL"),
    ("A1C CHECK","LAB MONITORING","ROUTINE","6 MONTHS"),
    ("DEPRESSION SCREENING","MENTAL HEALTH","ROUTINE","ANNUAL"),
    ("PTSD SCREENING","MENTAL HEALTH","ROUTINE","ANNUAL"),
    ("HEPATITIS C SCREENING","INFECTIOUS DISEASE","ROUTINE","ONCE"),
    ("PNEUMONIA VACCINE","IMMUNIZATION","ROUTINE","5 YEARS"),
    ("SMOKING CESSATION","HEALTH PROMOTION","HIGH","ONGOING"),
]


# ══════════════════════════════════════════════════════════════════════════════
#  USER DEFINITIONS (14 roles x 5 = 70 users, IENs 1-70)
# ══════════════════════════════════════════════════════════════════════════════

# Each role: (role_key, title, degree, service_section, user_class, provider_type, specialty, roles_str)
USER_ROLE_DEFS = [
    # PHYSICIAN (5)
    ("PHYSICIAN", [
        ("STAFF PHYSICIAN","MD","INTERNAL MEDICINE","PHYSICIAN","ATTENDING","INTERNAL MEDICINE","PROVIDER;CPRS;ORDER ENTRY"),
        ("STAFF PHYSICIAN","MD","FAMILY MEDICINE","PHYSICIAN","ATTENDING","FAMILY MEDICINE","PROVIDER;CPRS;ORDER ENTRY"),
        ("STAFF PHYSICIAN","DO","GERIATRICS","PHYSICIAN","ATTENDING","GERIATRICS","PROVIDER;CPRS;ORDER ENTRY"),
        ("STAFF PHYSICIAN","MD","HOSPITALIST","PHYSICIAN","ATTENDING","HOSPITAL MEDICINE","PROVIDER;CPRS;ORDER ENTRY"),
        ("STAFF PHYSICIAN","MD","EMERGENCY MEDICINE","PHYSICIAN","ATTENDING","EMERGENCY MEDICINE","PROVIDER;CPRS;ORDER ENTRY"),
    ]),
    # NURSE (5)
    ("NURSE", [
        ("REGISTERED NURSE","RN","MEDICAL-SURGICAL","NURSE","RN","MED-SURG NURSING","NURSE;CPRS;VITALS ENTRY"),
        ("REGISTERED NURSE","RN","ICU","NURSE","RN","CRITICAL CARE NURSING","NURSE;CPRS;VITALS ENTRY"),
        ("REGISTERED NURSE","BSN","PRIMARY CARE","NURSE","RN","AMBULATORY CARE NURSING","NURSE;CPRS;VITALS ENTRY"),
        ("NURSE PRACTITIONER","NP","WOMENS HEALTH","NURSE PRACTITIONER","ARNP","WOMENS HEALTH NP","PROVIDER;NURSE;CPRS;ORDER ENTRY"),
        ("REGISTERED NURSE","RN","MENTAL HEALTH","NURSE","RN","PSYCHIATRIC NURSING","NURSE;CPRS;VITALS ENTRY"),
    ]),
    # PHARMACIST (5)
    ("PHARMACIST", [
        ("CLINICAL PHARMACIST","PHARMD","PHARMACY","PHARMACIST","CLINICAL PHARMACIST","CLINICAL PHARMACY","PHARMACIST;CPRS;ORDER VERIFY"),
        ("CLINICAL PHARMACIST","PHARMD","ONCOLOGY PHARMACY","PHARMACIST","CLINICAL PHARMACIST","ONCOLOGY PHARMACY","PHARMACIST;CPRS;ORDER VERIFY"),
        ("STAFF PHARMACIST","RPH","PHARMACY","PHARMACIST","STAFF PHARMACIST","GENERAL PHARMACY","PHARMACIST;CPRS;ORDER VERIFY"),
        ("CLINICAL PHARMACIST","PHARMD","ANTICOAGULATION","PHARMACIST","CLINICAL PHARMACIST","ANTICOAGULATION","PHARMACIST;CPRS;ORDER VERIFY"),
        ("STAFF PHARMACIST","RPH","IV PHARMACY","PHARMACIST","STAFF PHARMACIST","IV PHARMACY","PHARMACIST;CPRS;ORDER VERIFY"),
    ]),
    # SURGEON (5)
    ("SURGEON", [
        ("STAFF SURGEON","MD","GENERAL SURGERY","PHYSICIAN","SURGEON","GENERAL SURGERY","PROVIDER;CPRS;ORDER ENTRY;SURGERY"),
        ("STAFF SURGEON","MD","ORTHOPEDIC SURGERY","PHYSICIAN","SURGEON","ORTHOPEDICS","PROVIDER;CPRS;ORDER ENTRY;SURGERY"),
        ("STAFF SURGEON","MD","CARDIAC SURGERY","PHYSICIAN","SURGEON","CARDIOTHORACIC","PROVIDER;CPRS;ORDER ENTRY;SURGERY"),
        ("STAFF SURGEON","MD","VASCULAR SURGERY","PHYSICIAN","SURGEON","VASCULAR","PROVIDER;CPRS;ORDER ENTRY;SURGERY"),
        ("STAFF SURGEON","MD","UROLOGY","PHYSICIAN","SURGEON","UROLOGY","PROVIDER;CPRS;ORDER ENTRY;SURGERY"),
    ]),
    # LAB_TECH (5)
    ("LAB_TECH", [
        ("LABORATORY TECHNOLOGIST","MT","CLINICAL CHEMISTRY","LAB TECH","MED TECH","CLINICAL CHEMISTRY","LAB;CPRS"),
        ("LABORATORY TECHNOLOGIST","MT","HEMATOLOGY","LAB TECH","MED TECH","HEMATOLOGY","LAB;CPRS"),
        ("LABORATORY TECHNOLOGIST","MT","MICROBIOLOGY","LAB TECH","MED TECH","MICROBIOLOGY","LAB;CPRS"),
        ("LABORATORY TECHNOLOGIST","MLT","BLOOD BANK","LAB TECH","MED TECH","BLOOD BANK","LAB;CPRS"),
        ("LABORATORY TECHNOLOGIST","MT","ANATOMIC PATHOLOGY","LAB TECH","MED TECH","ANATOMIC PATHOLOGY","LAB;CPRS"),
    ]),
    # RADIOLOGIST (5)
    ("RADIOLOGIST", [
        ("STAFF RADIOLOGIST","MD","DIAGNOSTIC RADIOLOGY","PHYSICIAN","RADIOLOGIST","DIAGNOSTIC RADIOLOGY","PROVIDER;CPRS;RADIOLOGY"),
        ("STAFF RADIOLOGIST","MD","INTERVENTIONAL RADIOLOGY","PHYSICIAN","RADIOLOGIST","INTERVENTIONAL RADIOLOGY","PROVIDER;CPRS;RADIOLOGY"),
        ("STAFF RADIOLOGIST","MD","NUCLEAR MEDICINE","PHYSICIAN","RADIOLOGIST","NUCLEAR MEDICINE","PROVIDER;CPRS;RADIOLOGY"),
        ("STAFF RADIOLOGIST","MD","NEURORADIOLOGY","PHYSICIAN","RADIOLOGIST","NEURORADIOLOGY","PROVIDER;CPRS;RADIOLOGY"),
        ("STAFF RADIOLOGIST","MD","MUSCULOSKELETAL RADIOLOGY","PHYSICIAN","RADIOLOGIST","MSK RADIOLOGY","PROVIDER;CPRS;RADIOLOGY"),
    ]),
    # REG_CLERK (5)
    ("REG_CLERK", [
        ("REGISTRATION CLERK","","ADMISSIONS","CLERK","REG CLERK","REGISTRATION","REGISTRATION;CPRS"),
        ("REGISTRATION CLERK","","ADMISSIONS","CLERK","REG CLERK","REGISTRATION","REGISTRATION;CPRS"),
        ("REGISTRATION CLERK","","ELIGIBILITY","CLERK","REG CLERK","ELIGIBILITY","REGISTRATION;CPRS"),
        ("REGISTRATION CLERK","","MEANS TEST","CLERK","REG CLERK","MEANS TEST","REGISTRATION;CPRS"),
        ("REGISTRATION CLERK","","SCHEDULING","CLERK","REG CLERK","SCHEDULING","REGISTRATION;CPRS;SCHEDULING"),
    ]),
    # BILLING (5)
    ("BILLING", [
        ("BILLING SPECIALIST","","REVENUE","BILLING","BILLING SPEC","BILLING","BILLING;CPRS"),
        ("BILLING SPECIALIST","","COLLECTIONS","BILLING","BILLING SPEC","COLLECTIONS","BILLING;CPRS"),
        ("BILLING SPECIALIST","","INSURANCE VERIFICATION","BILLING","BILLING SPEC","INSURANCE","BILLING;CPRS"),
        ("BILLING SUPERVISOR","","REVENUE","BILLING","BILLING SUPERVISOR","BILLING MANAGEMENT","BILLING;CPRS;REPORTS"),
        ("BILLING SPECIALIST","","THIRD PARTY","BILLING","BILLING SPEC","THIRD PARTY BILLING","BILLING;CPRS"),
    ]),
    # HIM (5)
    ("HIM", [
        ("HIM TECHNICIAN","RHIT","HEALTH INFORMATION","HIM","HIM TECH","CODING","HIM;CPRS;RECORDS"),
        ("HIM TECHNICIAN","RHIA","HEALTH INFORMATION","HIM","HIM TECH","RELEASE OF INFORMATION","HIM;CPRS;RECORDS;ROI"),
        ("HIM CODER","CCS","HEALTH INFORMATION","HIM","HIM CODER","INPATIENT CODING","HIM;CPRS;RECORDS"),
        ("HIM CODER","CPC","HEALTH INFORMATION","HIM","HIM CODER","OUTPATIENT CODING","HIM;CPRS;RECORDS"),
        ("HIM SUPERVISOR","RHIA","HEALTH INFORMATION","HIM","HIM SUPERVISOR","HIM MANAGEMENT","HIM;CPRS;RECORDS;REPORTS"),
    ]),
    # QUALITY (5)
    ("QUALITY", [
        ("QUALITY MANAGEMENT OFFICER","RN","QUALITY MANAGEMENT","QUALITY","QM OFFICER","PATIENT SAFETY","QUALITY;CPRS;REPORTS"),
        ("QUALITY ANALYST","MPH","QUALITY MANAGEMENT","QUALITY","QM ANALYST","QUALITY IMPROVEMENT","QUALITY;CPRS;REPORTS"),
        ("PATIENT SAFETY OFFICER","RN","QUALITY MANAGEMENT","QUALITY","SAFETY OFFICER","RISK MANAGEMENT","QUALITY;CPRS;REPORTS"),
        ("INFECTION CONTROL NURSE","RN","QUALITY MANAGEMENT","QUALITY","IC NURSE","INFECTION CONTROL","QUALITY;CPRS;REPORTS;INFECTION CONTROL"),
        ("QUALITY ANALYST","MS","QUALITY MANAGEMENT","QUALITY","QM ANALYST","PERFORMANCE MEASUREMENT","QUALITY;CPRS;REPORTS"),
    ]),
    # MH_PROVIDER (5)
    ("MH_PROVIDER", [
        ("STAFF PSYCHIATRIST","MD","MENTAL HEALTH","PHYSICIAN","PSYCHIATRIST","PSYCHIATRY","PROVIDER;CPRS;ORDER ENTRY;MH"),
        ("STAFF PSYCHIATRIST","MD","MENTAL HEALTH","PHYSICIAN","PSYCHIATRIST","GERIATRIC PSYCHIATRY","PROVIDER;CPRS;ORDER ENTRY;MH"),
        ("CLINICAL PSYCHOLOGIST","PHD","MENTAL HEALTH","PSYCHOLOGIST","PSYCHOLOGIST","CLINICAL PSYCHOLOGY","PROVIDER;CPRS;MH"),
        ("CLINICAL PSYCHOLOGIST","PSYD","MENTAL HEALTH","PSYCHOLOGIST","PSYCHOLOGIST","NEUROPSYCHOLOGY","PROVIDER;CPRS;MH"),
        ("CLINICAL PSYCHOLOGIST","PHD","MENTAL HEALTH","PSYCHOLOGIST","PSYCHOLOGIST","PTSD CLINICAL TEAM","PROVIDER;CPRS;MH"),
    ]),
    # SOCIAL_WORKER (5)
    ("SOCIAL_WORKER", [
        ("SOCIAL WORKER","LCSW","SOCIAL WORK","SOCIAL WORKER","LCSW","MEDICAL SOCIAL WORK","SOCIAL WORK;CPRS"),
        ("SOCIAL WORKER","LCSW","SOCIAL WORK","SOCIAL WORKER","LCSW","HOMELESS VETERAN PROGRAMS","SOCIAL WORK;CPRS"),
        ("SOCIAL WORKER","LCSW","SOCIAL WORK","SOCIAL WORKER","LCSW","CAREGIVER SUPPORT","SOCIAL WORK;CPRS"),
        ("SOCIAL WORKER","LCSW","SOCIAL WORK","SOCIAL WORKER","LCSW","MENTAL HEALTH SOCIAL WORK","SOCIAL WORK;CPRS;MH"),
        ("SOCIAL WORKER","MSW","SOCIAL WORK","SOCIAL WORKER","MSW","DISCHARGE PLANNING","SOCIAL WORK;CPRS"),
    ]),
    # DENTIST (5)
    ("DENTIST", [
        ("STAFF DENTIST","DDS","DENTAL","DENTIST","DENTIST","GENERAL DENTISTRY","PROVIDER;CPRS;DENTAL"),
        ("STAFF DENTIST","DMD","DENTAL","DENTIST","DENTIST","ORAL SURGERY","PROVIDER;CPRS;DENTAL;SURGERY"),
        ("STAFF DENTIST","DDS","DENTAL","DENTIST","DENTIST","PERIODONTICS","PROVIDER;CPRS;DENTAL"),
        ("STAFF DENTIST","DMD","DENTAL","DENTIST","DENTIST","PROSTHODONTICS","PROVIDER;CPRS;DENTAL"),
        ("DENTAL HYGIENIST","RDH","DENTAL","DENTAL HYGIENIST","DENTAL HYGIENIST","DENTAL HYGIENE","DENTAL;CPRS"),
    ]),
    # ADMIN (5)
    ("ADMIN", [
        ("SYSTEM ADMINISTRATOR","","INFORMATION TECHNOLOGY","ADMIN","SYS ADMIN","IT OPERATIONS","ADMIN;CPRS;ALL"),
        ("SYSTEM ADMINISTRATOR","","INFORMATION TECHNOLOGY","ADMIN","SYS ADMIN","SECURITY","ADMIN;CPRS;ALL;SECURITY"),
        ("FACILITY DIRECTOR","MHA","ADMINISTRATION","ADMIN","DIRECTOR","FACILITY MANAGEMENT","ADMIN;CPRS;ALL;REPORTS"),
        ("CHIEF OF STAFF","MD","ADMINISTRATION","ADMIN","COS","MEDICAL STAFF","ADMIN;CPRS;ALL;PROVIDER"),
        ("NURSE EXECUTIVE","DNP","NURSING ADMINISTRATION","ADMIN","NURSE EXEC","NURSING LEADERSHIP","ADMIN;CPRS;ALL;NURSE"),
    ]),
]

# User name pools (distinct from patient names to avoid confusion)
USER_LAST_NAMES = [
    "CHEN","PATEL","OCONNOR","ANDERSEN","NAKAMURA","IBRAHIM","KOWALSKI","DUBOIS",
    "ROSENBERG","MCCARTHY","GUPTA","JOHANSSON","KUZNETSOV","OBRIEN","YAMAMOTO",
    "SCHUSTER","KAPOOR","LINDGREN","ROMANO","FITZGERALD","NGUYEN","BERGSTROM",
    "SINGH","OMALLEY","TANAKA","HASSAN","VOLKOV","DELUCA","GOLDBERG","MURPHY",
    "SHARMA","LARSEN","PETROV","CALLAHAN","SUZUKI","FAROUK","NOVAK","BIANCHI",
    "WEINSTEIN","DONOVAN","AGRAWAL","MAGNUSSON","POPOV","GALLAGHER","WATANABE",
    "KHALIL","KOVACS","ESPOSITO","RUBIN","BRENNAN","MEHTA","NILSSON","KOZLOV",
    "SULLIVAN","TAKAHASHI","MANSOUR","SZABO","MORETTI","SEGAL","FLANAGAN",
    "DESAI","HOLMBERG","SOKOLOV","QUINLAN","HAYASHI","SALEH","HORVAT","GALLO",
    "ROSEN","CASEY",
]

USER_MALE_FIRST = [
    "JAMES","MICHAEL","ROBERT","DAVID","WILLIAM","JOHN","RICHARD","THOMAS",
    "STEVEN","ANDREW","DANIEL","CHRISTOPHER","KEVIN","BRIAN","MARK","TIMOTHY",
    "JASON","JEFFREY","SCOTT","BENJAMIN","RAYMOND","GREGORY","PAUL","NATHAN",
    "JONATHAN","RYAN","ADAM","PETER","ERIC","KENNETH","SAMUEL","PATRICK",
    "PHILIP","ALEXANDER","MARCUS","OWEN",
]

USER_FEMALE_FIRST = [
    "JENNIFER","SARAH","ELIZABETH","MICHELLE","JESSICA","AMANDA","CATHERINE",
    "LAURA","STEPHANIE","KAREN","REBECCA","SUSAN","PATRICIA","ANGELA","CHRISTINA",
    "LISA","DIANE","MARIA","ANNA","RACHEL","JULIE","NICOLE","HEATHER","EMILY",
    "MARGARET","ALLISON","KATHERINE","MEGAN","CAROLYN","TERESA","DENISE","ROBIN",
    "ANDREA","SANDRA","JACQUELINE","VICTORIA",
]


# ── Helper functions ───────────────────────────────────────────────────────

def fm_date(year, month, day):
    """FileMan date: (year-1700)*10000 + month*100 + day"""
    return (year - 1700) * 10000 + month * 100 + day

def fm_datetime(year, month, day, hour, minute):
    frac = hour / 100 + minute / 10000
    return f"{fm_date(year, month, day)}.{hour:02d}{minute:02d}"

def random_fm_dob(min_age=25, max_age=90):
    age = random.randint(min_age, max_age)
    year = 2026 - age
    month = random.randint(1, 12)
    day = random.randint(1, 28)
    return fm_date(year, month, day)

def random_fm_service_dates():
    entry_year = random.randint(1960, 2015)
    sep_year = entry_year + random.randint(2, 8)
    em = random.randint(1, 12)
    ed = random.randint(1, 28)
    sm = random.randint(1, 12)
    sd = random.randint(1, 28)
    return fm_date(entry_year, em, ed), fm_date(sep_year, sm, sd)

def random_fm_clinical_date():
    year = random.choice([2024, 2025])
    month = random.randint(1, 12)
    day = random.randint(1, 28)
    return fm_date(year, month, day)

def random_fm_recent_date():
    """A date in the last 6 months (late 2025 / early 2026)."""
    year = random.choice([2025, 2025, 2025, 2026])
    if year == 2026:
        month = random.randint(1, 3)
    else:
        month = random.randint(7, 12)
    day = random.randint(1, 28)
    return fm_date(year, month, day)

def random_fm_onset():
    year = random.randint(2008, 2025)
    month = random.randint(1, 12)
    day = random.randint(1, 28)
    return fm_date(year, month, day)

def random_fm_visit():
    year = 2025
    month = random.randint(6, 9)
    day = random.randint(1, 28)
    hour = random.randint(7, 15)
    minute = random.choice([0, 15, 30, 45])
    return fm_date(year, month, day), f"{hour:02d}{minute:02d}"

def random_fm_future_date():
    """A date within the next year for reminders."""
    year = random.choice([2026, 2027])
    month = random.randint(1, 12)
    day = random.randint(1, 28)
    return fm_date(year, month, day)

def abnormal_flag(val, ref_low, ref_high):
    try:
        v = float(val)
        lo = float(ref_low)
        hi = float(ref_high)
        if v > hi: return "H"
        if v < lo: return "L"
    except:
        pass
    return ""

def patient_has_icd10(patient, icd10_set):
    """Check if patient has any problem with ICD10 in the given set."""
    return any(icd in icd10_set for _, icd, _, _ in patient["problems"])

def get_lot_number():
    """Generate a plausible vaccine lot number."""
    prefix = random.choice(["EW","FK","GH","JN","LP","MR","PQ","RS","TU","VW"])
    num = random.randint(1000, 9999)
    return f"{prefix}{num}"


# ══════════════════════════════════════════════════════════════════════════════
#  GENERATE USERS (IENs 1-70)
# ══════════════════════════════════════════════════════════════════════════════

print("Generating 70 users (14 roles x 5)...")

users = []
user_ien = 0
used_user_names = set()

# Build role -> IEN mapping for care team assignment
role_ien_map = {}  # role_key -> list of IENs

for role_key, role_defs in USER_ROLE_DEFS:
    role_ien_map[role_key] = []
    for title, degree, service, user_class, provider_type, specialty, roles_str in role_defs:
        user_ien += 1
        # Pick unique name
        sex = "M" if user_ien % 2 == 1 else "F"
        while True:
            last = random.choice(USER_LAST_NAMES)
            first = random.choice(USER_MALE_FIRST if sex == "M" else USER_FEMALE_FIRST)
            uname = f"{last},{first}"
            if uname not in used_user_names:
                used_user_names.add(uname)
                break

        users.append({
            "ien": user_ien,
            "name": uname,
            "title": title,
            "degree": degree,
            "service": service,
            "user_class": user_class,
            "provider_type": provider_type,
            "specialty": specialty,
            "roles_str": roles_str,
            "role_key": role_key,
        })
        role_ien_map[role_key].append(user_ien)

assert user_ien == 70, f"Expected 70 users, got {user_ien}"

# ── Write users.zwr ────────────────────────────────────────────────────────

print("Writing users.zwr...")
with open(os.path.join(OUT_DIR, "users.zwr"), "w", newline="\n") as f:
    f.write("; VistA NEW PERSON file #200 (^VA(200,...)) — 70 synthetic staff users\n")
    f.write("; 14 roles x 5 users each: PHYSICIAN, NURSE, PHARMACIST, SURGEON, LAB_TECH,\n")
    f.write(";   RADIOLOGIST, REG_CLERK, BILLING, HIM, QUALITY, MH_PROVIDER, SOCIAL_WORKER, DENTIST, ADMIN\n")
    f.write('; Node 0: Name^Title^Degree^ServiceSection\n')
    f.write('; Node .1: UserClass^ProviderType^Specialty\n')
    f.write('; Node .13: Role1;Role2;Role3\n')
    f.write(";\n")
    for u in users:
        i = u["ien"]
        f.write(f'^VA(200,{i},0)="{u["name"]}^{u["title"]}^{u["degree"]}^{u["service"]}"\n')
        f.write(f'^VA(200,{i},.1)="{u["user_class"]}^{u["provider_type"]}^{u["specialty"]}"\n')
        f.write(f'^VA(200,{i},.13)="{u["roles_str"]}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  GENERATE PATIENTS
# ══════════════════════════════════════════════════════════════════════════════

print(f"Generating {patient_count} patients...")

patients = []
used_names = set()

for ien in range(1, patient_count + 1):
    sex = "M" if ien % 2 == 1 else "F"

    while True:
        last = random.choice(LAST_NAMES)
        first = random.choice(MALE_FIRST if sex == "M" else FEMALE_FIRST)
        name = f"{last},{first}"
        if name not in used_names:
            used_names.add(name)
            break

    dob = random_fm_dob()
    ssn = f"9001{ien:05d}P"

    city, state, zipcode = random.choice(CITIES_STATES_ZIPS)
    area = random.choice(AREA_CODES)
    street_num = random.randint(100, 999)
    street_name = random.choice(STREETS)
    street_type = random.choice(STREET_TYPES)

    phone_base = f"{area}-555-{ien:04d}"
    work_phone = f"{area}-555-{(ien + 5000):04d}" if ien + 5000 <= 9999 else f"{area}-555-{ien % 10000:04d}"

    emerg_rel = random.choice(EMERG_RELS)
    emerg_first = random.choice(MALE_FIRST if emerg_rel in ("SPOUSE","BROTHER","FATHER","SON","GRANDSON","FRIEND") and sex == "F"
                                else FEMALE_FIRST if emerg_rel in ("SPOUSE","SISTER","MOTHER","DAUGHTER","GRANDDAUGHTER","FRIEND") and sex == "M"
                                else MALE_FIRST)
    emerg_name = f"{last},{emerg_first}"
    emerg_phone = f"{area}-555-{(ien + 3000) % 10000:04d}"

    branch = random.choice(BRANCHES)
    entry_fm, sep_fm = random_fm_service_dates()
    discharge = random.choices(["HONORABLE","GENERAL","OTHER THAN HONORABLE"], weights=[85,10,5])[0]
    pow_flag = "Y" if random.random() < 0.03 else "N"

    sc_pct = random.choice([0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100])
    if sc_pct == 0:
        elig = "NSC VETERAN"
        prim_elig = "NSC"
        vet = "Y"
    elif sc_pct < 50:
        elig = "SC VETERAN"
        prim_elig = "SC LESS THAN 50%"
        vet = "Y"
    else:
        elig = "SC VETERAN"
        prim_elig = "SC 50% TO 100%"
        vet = "Y"

    # ~40% of patients get assigned a clinical profile for coherent stories
    # Distribute profiles evenly across the patient population
    profile = None
    if ien <= len(CLINICAL_PROFILES):
        # First N patients each get a unique profile (ensures all profiles are represented)
        profile = CLINICAL_PROFILES[ien - 1]
    elif random.random() < 0.25:
        # Additional random patients also get profiles
        profile = random.choice(CLINICAL_PROFILES)

    if profile:
        patient_problems = list(profile["problems"])
        # Some profile patients also get 1-2 additional random chronic conditions
        if random.random() < 0.6:
            extra = random.sample(PROBLEM_POOL, min(random.randint(1, 2), len(PROBLEM_POOL)))
            # Avoid duplicates by ICD10
            existing_icds = {icd for _, icd, _, _ in patient_problems}
            for prob in extra:
                if prob[1] not in existing_icds:
                    patient_problems.append(prob)
    else:
        # Non-profile patients: more conditions than before (2-5 instead of 1-4)
        num_problems = random.choices([2, 3, 4, 5], weights=[20, 35, 30, 15])[0]
        patient_problems = random.sample(PROBLEM_POOL, min(num_problems, len(PROBLEM_POOL)))

    num_meds = random.choices([2, 3, 4, 5], weights=[20, 35, 30, 15])[0]
    patient_meds = random.sample(MED_POOL, min(num_meds, len(MED_POOL)))

    # Assign a PCP from PHYSICIAN pool (used by care_team and as provider_dfn)
    pcp_ien = random.choice(role_ien_map["PHYSICIAN"])

    patients.append({
        "ien": ien, "name": name, "sex": sex, "dob": dob, "ssn": ssn,
        "street": f"{street_num} {street_name} {street_type}",
        "city": city, "state": state, "zip": zipcode,
        "phone": phone_base, "work_phone": work_phone,
        "emerg_name": emerg_name, "emerg_rel": emerg_rel, "emerg_phone": emerg_phone,
        "vet": vet, "sc_pct": sc_pct, "elig": elig, "prim_elig": prim_elig,
        "entry_fm": entry_fm, "sep_fm": sep_fm, "branch": branch, "discharge": discharge, "pow": pow_flag,
        "problems": patient_problems, "meds": patient_meds,
        "provider_dfn": pcp_ien,
        "pcp_ien": pcp_ien,
        "profile": profile,
    })


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE patients.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing patients.zwr...")
with open(os.path.join(OUT_DIR, "patients.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA PATIENT file #2 (^DPT) — {patient_count} synthetic test patients\n")
    f.write("; Format: ^DPT(IEN,node)=\"piece1^piece2^...\"\n")
    f.write("; Node 0: Name^Sex^DOB(FM)^SSN\n")
    f.write("; Node .11: Street1^Street2^City^State^Zip\n")
    f.write("; Node .13: Phone (residence)\n")
    f.write("; Node .132: Phone (work)\n")
    f.write("; Node .33: EmergContactName^Relationship^Phone\n")
    f.write("; Node .36: Veteran^SC%^Eligibility^PrimaryEligibility\n")
    f.write("; Node .32: EntryDate(FM)^SepDate(FM)^Branch^DischargeType^POW\n")
    f.write(";\n")
    for p in patients:
        i = p["ien"]
        f.write(f'^DPT({i},0)="{p["name"]}^{p["sex"]}^{p["dob"]}^{p["ssn"]}"\n')
        f.write(f'^DPT({i},.11)="{p["street"]}^^{p["city"]}^{p["state"]}^{p["zip"]}"\n')
        f.write(f'^DPT({i},.13)="{p["phone"]}"\n')
        f.write(f'^DPT({i},.132)="{p["work_phone"]}"\n')
        f.write(f'^DPT({i},.33)="{p["emerg_name"]}^{p["emerg_rel"]}^{p["emerg_phone"]}"\n')
        f.write(f'^DPT({i},.36)="{p["vet"]}^{p["sc_pct"]}^{p["elig"]}^{p["prim_elig"]}"\n')
        f.write(f'^DPT({i},.32)="{p["entry_fm"]}^{p["sep_fm"]}^{p["branch"]}^{p["discharge"]}^{p["pow"]}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE care_team.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing care_team.zwr...")
care_team_ien = 0
care_team_count = 0
with open(os.path.join(OUT_DIR, "care_team.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA PCMM TEAM POSITION ASSIGNMENTS file #404.43 (^PCMM(404.43,...)) — {patient_count}-patient care teams\n")
    f.write('; Format: ^PCMM(404.43,IEN,0)="PatientDFN;DPT(^ProviderDFN;VA(200,^Role^Specialty^IsPCP^Source"\n')
    f.write(";\n")
    for p in patients:
        patient_icd10s = {icd for _, icd, _, _ in p["problems"]}
        has_mh = bool(patient_icd10s & MH_ICD10S)
        has_cardiac = bool(patient_icd10s & CARDIAC_ICD10S)

        team_members = []

        # 1. PCP (always)
        pcp = p["pcp_ien"]
        pcp_user = next(u for u in users if u["ien"] == pcp)
        team_members.append((pcp, "PRIMARY CARE PROVIDER", pcp_user["specialty"], "1", "PCMM"))

        # 2. Nurse (always)
        nurse_ien = random.choice(role_ien_map["NURSE"])
        nurse_user = next(u for u in users if u["ien"] == nurse_ien)
        team_members.append((nurse_ien, "REGISTERED NURSE", nurse_user["specialty"], "0", "PCMM"))

        # 3. Problem-correlated specialists (1-2)
        if has_mh:
            mh_ien = random.choice(role_ien_map["MH_PROVIDER"])
            mh_user = next(u for u in users if u["ien"] == mh_ien)
            team_members.append((mh_ien, "MENTAL HEALTH PROVIDER", mh_user["specialty"], "0", "PCMM"))

        if has_cardiac:
            surg_ien = random.choice(role_ien_map["SURGEON"])
            surg_user = next(u for u in users if u["ien"] == surg_ien)
            team_members.append((surg_ien, "SPECIALIST", surg_user["specialty"], "0", "CONSULT"))

        # 4. Sometimes a social worker (20%) or pharmacist (15%)
        if random.random() < 0.20:
            sw_ien = random.choice(role_ien_map["SOCIAL_WORKER"])
            sw_user = next(u for u in users if u["ien"] == sw_ien)
            team_members.append((sw_ien, "SOCIAL WORKER", sw_user["specialty"], "0", "PCMM"))

        if random.random() < 0.15:
            pharm_ien = random.choice(role_ien_map["PHARMACIST"])
            pharm_user = next(u for u in users if u["ien"] == pharm_ien)
            team_members.append((pharm_ien, "CLINICAL PHARMACIST", pharm_user["specialty"], "0", "PCMM"))

        # Ensure 3-5 members — if fewer than 3, add another specialist
        while len(team_members) < 3:
            extra_role = random.choice(["PHARMACIST","SOCIAL_WORKER","MH_PROVIDER"])
            extra_ien = random.choice(role_ien_map[extra_role])
            extra_user = next(u for u in users if u["ien"] == extra_ien)
            # Avoid duplicates
            if extra_ien not in [m[0] for m in team_members]:
                team_members.append((extra_ien, extra_user["title"], extra_user["specialty"], "0", "PCMM"))

        # Cap at 5
        team_members = team_members[:5]

        for provider_ien, role, specialty, is_pcp, source in team_members:
            care_team_ien += 1
            care_team_count += 1
            f.write(f'^PCMM(404.43,{care_team_ien},0)="{p["ien"]};DPT(^{provider_ien};VA(200,^{role}^{specialty}^{is_pcp}^{source}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE allergies.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing allergies.zwr...")
allergy_ien = 0
with open(os.path.join(OUT_DIR, "allergies.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA PATIENT ALLERGIES file #120.8 (^GMR(120.8,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^GMR(120.8,IEN,node)="piece1^piece2^..."\n')
    f.write("; Node 0: Allergen^AllergenType^Reactant^PatientDFN(;ptr)^^^ObservedHistorical\n")
    f.write("; Node 10,n,0: Reaction sign/symptom\n")
    f.write("; Node 14.5: Severity\n")
    f.write(";\n")
    for p in patients:
        num_allergies = random.choices([0, 1, 2, 3], weights=[15, 45, 30, 10])[0]
        if num_allergies == 0:
            continue
        all_allergens = ALLERGY_DRUGS + ALLERGY_CLASSES + ALLERGY_OTHER
        chosen = random.sample(all_allergens, min(num_allergies, len(all_allergens)))
        for allergen_name, allergen_type, reactant in chosen:
            allergy_ien += 1
            obs = random.choice(["OBSERVED", "HISTORICAL"])
            sev = random.choice(SEVERITIES)
            num_reactions = random.randint(1, 3)
            reactions = random.sample(ALLERGY_REACTIONS, num_reactions)

            f.write(f'; --- Patient {p["ien"]} ({p["name"]}) ---\n')
            f.write(f'^GMR(120.8,{allergy_ien},0)="{allergen_name}^{allergen_type}^{reactant}^{p["ien"]};DPT(^^^{obs}"\n')
            for ri, rxn in enumerate(reactions, 1):
                f.write(f'^GMR(120.8,{allergy_ien},10,{ri},0)="{rxn}"\n')
            f.write(f'^GMR(120.8,{allergy_ien},14.5)="{sev}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE problems.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing problems.zwr...")
prob_ien = 0
with open(os.path.join(OUT_DIR, "problems.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA PROBLEM LIST file #9000011 (^AUPNPROB) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^AUPNPROB(IEN,node)="piece1^piece2^..."\n')
    f.write("; Node 0: Diagnosis^Condition^DateOnset(FM)^Status^PatientDFN(;ptr)\n")
    f.write("; Node 1: DiagnosisCode^Priority^ServiceConnected\n")
    f.write(";\n")
    for p in patients:
        for (cond, icd, chron, sc_prob) in p["problems"]:
            prob_ien += 1
            onset = random_fm_onset()
            status = "ACTIVE" if random.random() < 0.9 else "INACTIVE"
            sc_flag = "1" if random.random() < sc_prob and p["sc_pct"] > 0 else "0"
            f.write(f'^AUPNPROB({prob_ien},0)="{cond}^{chron}^{onset}^{status}^{p["ien"]};DPT("\n')
            f.write(f'^AUPNPROB({prob_ien},1)="{icd}^{chron}^{sc_flag}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE orders.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing orders.zwr...")
order_ien = 0
with open(os.path.join(OUT_DIR, "orders.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA ORDER file #100 (^OR(100,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^OR(100,IEN,node)="piece1^piece2^..."\n')
    f.write("; Node 0: Status^PatientDFN(;ptr)^ProviderDFN^StartDate(FM)^^Urgency\n")
    f.write("; Node 1: OrderType^OrderableItem\n")
    f.write(";\n")
    for p in patients:
        num_orders = random.choices([1, 2, 3], weights=[30, 45, 25])[0]
        for _ in range(num_orders):
            order_ien += 1
            status = random.choices(["ACTIVE","COMPLETE","DISCONTINUED"], weights=[60,30,10])[0]
            urgency = random.choices(["ROUTINE","STAT"], weights=[90,10])[0]
            start_date = random_fm_clinical_date()

            order_cat = random.choices(["PHARMACY","LAB","CONSULT"], weights=[60,25,15])[0]
            if order_cat == "PHARMACY":
                item = random.choice(ORDER_TYPES_PHARMACY)
            elif order_cat == "LAB":
                item = random.choice(ORDER_TYPES_LAB)
            else:
                item = random.choice(ORDER_TYPES_CONSULT)

            f.write(f'^OR(100,{order_ien},0)="{status}^{p["ien"]};DPT(^{p["provider_dfn"]}^{start_date}^^{urgency}"\n')
            f.write(f'^OR(100,{order_ien},1)="{order_cat}^{item}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE labs.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing labs.zwr...")
# Condition-correlated lab generators
def diabetic_labs():
    """Generate labs typical of a diabetic patient."""
    return {
        "GLUCOSE": random.randint(130, 250),
        "HBA1C": round(random.uniform(7.0, 10.5), 1),
        "BUN": random.randint(15, 35),
        "CREATININE": round(random.uniform(0.9, 2.2), 1),
        "POTASSIUM": round(random.uniform(3.8, 5.3), 1),
        "GFR": random.randint(30, 80),
    }

def cardiac_labs():
    """Generate labs typical of a cardiac patient."""
    return {
        "BNP": random.randint(150, 800),
        "TOTAL CHOLESTEROL": random.randint(180, 280),
        "LDL": random.randint(90, 180),
        "TRIGLYCERIDES": random.randint(120, 300),
        "POTASSIUM": round(random.uniform(3.5, 5.2), 1),
        "SODIUM": random.randint(132, 145),
        "INR": round(random.uniform(1.8, 3.5), 1),
    }

def ckd_labs():
    """Generate labs typical of CKD patient."""
    return {
        "CREATININE": round(random.uniform(1.5, 4.0), 1),
        "BUN": random.randint(25, 60),
        "GFR": random.randint(15, 45),
        "POTASSIUM": round(random.uniform(4.2, 5.8), 1),
        "CALCIUM": round(random.uniform(7.8, 9.5), 1),
        "ALBUMIN": round(random.uniform(2.5, 3.8), 1),
    }

def anemia_labs():
    """Generate labs typical of iron deficiency anemia."""
    return {
        "HEMOGLOBIN": round(random.uniform(8.0, 11.5), 1),
        "IRON": random.randint(20, 55),
        "FERRITIN": random.randint(5, 20),
    }

with open(os.path.join(OUT_DIR, "labs.zwr"), "w", newline="\n") as f:
    f.write(f'; VistA LAB DATA file #63 (^LR(63,...)) — {patient_count}-patient synthetic Chemistry (CH) results\n')
    f.write('; Format: ^LR(63,PatientDFN,"CH",FMDate,Seq)="^^^TestName^Value^Units^RefLow^RefHigh^AbnFlag"\n')
    f.write("; Note: DFN matches patient IEN from ^DPT\n")
    f.write(";\n")
    for p in patients:
        patient_icd10s = {icd for _, icd, _, _ in p["problems"]}
        has_dm = bool(patient_icd10s & {"E11.9","E11.40","E11.22"})
        has_cardiac = bool(patient_icd10s & CARDIAC_ICD10S)
        has_ckd = bool(patient_icd10s & {"N18.3","E11.22"})
        has_anemia = "D50.9" in patient_icd10s

        # 2-3 lab sets per patient (trending data)
        num_lab_sets = random.choices([2, 3], weights=[50, 50])[0]
        for lab_set_idx in range(num_lab_sets):
            lab_date = random_fm_clinical_date()
            seq = 0

            # Start with condition-correlated labs
            correlated_results = {}
            if has_dm:
                correlated_results.update(diabetic_labs())
            if has_cardiac:
                correlated_results.update(cardiac_labs())
            if has_ckd:
                correlated_results.update(ckd_labs())
            if has_anemia:
                correlated_results.update(anemia_labs())

            # Write correlated labs first
            for test_name, val in correlated_results.items():
                if test_name in LAB_TESTS:
                    units, ref_lo, ref_hi, _ = LAB_TESTS[test_name]
                    flag = abnormal_flag(val, ref_lo, ref_hi)
                    seq += 1
                    f.write(f'^LR(63,{p["ien"]},"CH",{lab_date},{seq})="^^^{test_name}^{val}^{units}^{ref_lo}^{ref_hi}^{flag}"\n')

            # Add random additional labs
            num_random = random.randint(2, 4)
            available = [t for t in LAB_TESTS.keys() if t not in correlated_results]
            random_tests = random.sample(available, min(num_random, len(available)))
            for test_name in random_tests:
                units, ref_lo, ref_hi, gen_val = LAB_TESTS[test_name]
                val = gen_val()
                flag = abnormal_flag(val, ref_lo, ref_hi)
                seq += 1
                f.write(f'^LR(63,{p["ien"]},"CH",{lab_date},{seq})="^^^{test_name}^{val}^{units}^{ref_lo}^{ref_hi}^{flag}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE vitals.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing vitals.zwr...")
vital_ien = 0
with open(os.path.join(OUT_DIR, "vitals.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA GMRV VITAL MEASUREMENT file #120.5 (^GMR(120.5,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^GMR(120.5,IEN,0)="DateTimeTaken(FM)^VitalType^Value^PatientDFN(;ptr)"\n')
    f.write("; Optional qualifier sub-nodes: ^GMR(120.5,IEN,5,n,0)=\"Qualifier\"\n")
    f.write(";\n")
    for p in patients:
        # 2-4 vital sets per patient (trending data over different dates)
        num_vital_sets = random.choices([2, 3, 4], weights=[30, 45, 25])[0]
        # Generate a stable base weight and height for the patient
        base_weight = random.randint(120, 240)
        height_inches = random.randint(62, 76)
        has_htn = any(icd in ("I10",) for _, icd, _, _ in p["problems"])
        has_pain_condition = any(icd in ("G89.29","M54.5","M25.569","M51.36","M79.7") for _, icd, _, _ in p["problems"])
        has_copd = any(icd in ("J44.1",) for _, icd, _, _ in p["problems"])

        for vital_set_idx in range(num_vital_sets):
            date_fm, time_str = random_fm_visit()
            dt = f"{date_fm}.{time_str}"

            # BP — higher if hypertensive
            if has_htn:
                sys_bp = random.randint(130, 180)
                dia_bp = random.randint(78, 105)
            else:
                sys_bp = random.randint(100, 145)
                dia_bp = random.randint(60, 88)
            vital_ien += 1
            f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^BLOOD PRESSURE^{sys_bp}/{dia_bp}^{p["ien"]};DPT("\n')

            pulse = random.randint(55, 100)
            vital_ien += 1
            f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^PULSE^{pulse}^{p["ien"]};DPT("\n')

            # Weight varies slightly around base
            weight = base_weight + random.randint(-5, 5)
            vital_ien += 1
            f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^WEIGHT^{weight}^{p["ien"]};DPT("\n')

            # Height — only on first set
            if vital_set_idx == 0:
                vital_ien += 1
                f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^HEIGHT^{height_inches}^{p["ien"]};DPT("\n')

            # Temperature
            if random.random() < 0.5:
                temp = round(random.uniform(97.0, 99.2), 1)
                vital_ien += 1
                f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^TEMPERATURE^{temp}^{p["ien"]};DPT("\n')

            # Respiration
            if random.random() < 0.5:
                resp = random.randint(14, 22)
                vital_ien += 1
                f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^RESPIRATION^{resp}^{p["ien"]};DPT("\n')

            # SpO2 — lower if COPD
            if random.random() < 0.6:
                if has_copd:
                    spo2 = random.randint(88, 96)
                else:
                    spo2 = random.randint(94, 100)
                vital_ien += 1
                f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^PULSE OXIMETRY^{spo2}^{p["ien"]};DPT("\n')

            # Pain — higher if pain condition
            if has_pain_condition or random.random() < 0.2:
                if has_pain_condition:
                    pain = random.randint(3, 8)
                else:
                    pain = random.randint(0, 4)
                loc = random.choice(PAIN_LOCATIONS)
                vital_ien += 1
                f.write(f'^GMR(120.5,{vital_ien},0)="{dt}^PAIN^{pain}^{p["ien"]};DPT("\n')
                f.write(f'^GMR(120.5,{vital_ien},5,1,0)="{loc}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE tiu.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing tiu.zwr...")
tiu_ien = 0
with open(os.path.join(OUT_DIR, "tiu.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA TIU DOCUMENT file #8925 (^TIU(8925,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^TIU(8925,IEN,0)="DocumentType^PatientDFN(;ptr)^AuthorDFN^^^^ReferenceDate(FM)"\n')
    f.write('; Text: ^TIU(8925,IEN,"TEXT",line,0)="line of text"\n')
    f.write(";\n")
    for p in patients:
        profile = p.get("profile")

        # Every patient gets 2-5 notes
        num_notes = random.choices([2, 3, 4, 5], weights=[20, 35, 30, 15])[0]
        notes_written = 0

        # If patient has a profile, write profile-specific notes first
        if profile and profile["name"] in PROFILE_NOTE_TEMPLATES:
            profile_notes = PROFILE_NOTE_TEMPLATES[profile["name"]]
            for template_title, template_lines in profile_notes:
                if notes_written >= num_notes:
                    break
                tiu_ien += 1
                notes_written += 1
                ref_date = random_fm_clinical_date()
                # PT notes are progress notes
                if "PT " in template_title or "PHYSICAL THERAPY" in template_title:
                    doc_type = "PROGRESS NOTE"
                elif "DISCHARGE" in template_title or "REHABILITATION" in template_title:
                    doc_type = "DISCHARGE SUMMARY"
                elif "COMPREHENSIVE" in template_title or "ANNUAL" in template_title:
                    doc_type = "PROGRESS NOTE"
                else:
                    doc_type = random.choices(["PROGRESS NOTE","CONSULT NOTE"], weights=[80,20])[0]

                f.write(f'^TIU(8925,{tiu_ien},0)="{doc_type}^{p["ien"]};DPT(^{p["provider_dfn"]}^^^^{ref_date}"\n')
                f.write(f'^TIU(8925,{tiu_ien},"TEXT",1,0)="{template_title}"\n')
                for li, line in enumerate(template_lines, 2):
                    f.write(f'^TIU(8925,{tiu_ien},"TEXT",{li},0)="{line}"\n')

        # Fill remaining notes with generic templates
        while notes_written < num_notes:
            tiu_ien += 1
            notes_written += 1
            ref_date = random_fm_clinical_date()
            doc_type = random.choices(["PROGRESS NOTE","TELEPHONE NOTE","DISCHARGE SUMMARY"],
                                       weights=[75,15,10])[0]
            template_title, template_lines = random.choice(NOTE_TEMPLATES)

            f.write(f'^TIU(8925,{tiu_ien},0)="{doc_type}^{p["ien"]};DPT(^{p["provider_dfn"]}^^^^{ref_date}"\n')
            f.write(f'^TIU(8925,{tiu_ien},"TEXT",1,0)="{template_title}"\n')
            for li, line in enumerate(template_lines, 2):
                f.write(f'^TIU(8925,{tiu_ien},"TEXT",{li},0)="{line}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE consults.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing consults.zwr...")
consult_ien = 0
# Build doctor names from user pool (physicians + surgeons + MH providers)
DOCTOR_NAMES = [u["name"].replace(",", ", ") for u in users if u["role_key"] in ("PHYSICIAN","SURGEON","MH_PROVIDER")]

# Profile-specific consult reasons (more detailed than generic pool)
PROFILE_CONSULT_REASONS = {
    "TIBIAL_FRACTURE": [
        ("PHYSICAL THERAPY", "ROUTINE", "Post-operative rehabilitation following ORIF left tibial shaft fracture. Patient 6 weeks post-op, transitioning to weight-bearing. Needs gait training, ROM restoration, and strengthening program. 2-3x/week x 8 weeks."),
        ("ORTHOPEDICS", "ROUTINE", "Follow-up evaluation of left tibial shaft fracture status post ORIF. Progressive callus formation on x-ray. Needs weight-bearing advancement guidance."),
        ("PAIN MANAGEMENT", "ROUTINE", "Chronic pain management for patient with tibial fracture. Opioid taper needed. Consider multimodal approach."),
    ],
    "HIP_FRACTURE": [
        ("PHYSICAL THERAPY", "ROUTINE", "Post-operative rehabilitation following left hip hemiarthroplasty. Posterior hip precautions. Gait training, strengthening, balance, and fall prevention. 2x/week x 6 weeks."),
        ("GERIATRICS", "ROUTINE", "Falls risk evaluation. Patient sustained hip fracture from mechanical fall. Need comprehensive geriatric assessment and bone health optimization."),
        ("ENDOCRINOLOGY", "ROUTINE", "Osteoporosis management. DEXA T-score -2.8. Need treatment initiation to prevent future fragility fractures."),
    ],
    "ANKLE_FRACTURE": [
        ("PHYSICAL THERAPY", "ROUTINE", "Post-operative rehabilitation following ORIF left bimalleolar ankle fracture. Transitioning to CAM boot. Needs ROM, strengthening, proprioception, and gait training. 2-3x/week x 8 weeks."),
        ("ORTHOPEDICS", "ROUTINE", "Follow-up evaluation of left ankle bimalleolar fracture status post ORIF. Needs weight-bearing advancement."),
    ],
    "COMPLEX_DIABETIC": [
        ("NEPHROLOGY", "ROUTINE", "CKD progression in diabetic patient. eGFR declining (42 from 55 last year). Proteinuria 180 mg/g. Need management optimization."),
        ("OPHTHALMOLOGY", "ROUTINE", "Diabetic retinopathy screening. Mild NPDR noted on prior exam. Needs follow-up evaluation."),
        ("PODIATRY", "ROUTINE", "Diabetic foot care. Peripheral neuropathy with decreased monofilament sensation. Skin cracking and thickened nails."),
        ("NUTRITION", "ROUTINE", "Diabetes diet counseling. A1C 9.1%. Needs carb counting instruction and meal planning assistance."),
    ],
    "POLYTRAUMA": [
        ("PHYSICAL THERAPY", "ROUTINE", "Chronic low back pain management. Core stabilization program. Pain currently 6/10."),
        ("AUDIOLOGY", "ROUTINE", "Bilateral sensorineural hearing loss. Hearing aid evaluation and fitting needed. Also tinnitus assessment."),
        ("PSYCHOLOGY", "ROUTINE", "PTSD evaluation and CPT therapy. PCL-5 score 48. Nightmares 3-4x/week. Hypervigilance."),
        ("NEUROLOGY", "ROUTINE", "TBI follow-up. Persistent post-concussive symptoms. MoCA 24/30. Neuropsych testing recommended."),
    ],
    "ACL_TEAR": [
        ("PHYSICAL THERAPY", "ROUTINE", "Post-operative ACL reconstruction rehabilitation. BTB autograft. Phase 1 protocol. 3x/week x 6 weeks, then reassess through all phases. Total ~6-9 months rehabilitation."),
        ("ORTHOPEDICS", "ROUTINE", "Post-operative follow-up right ACL reconstruction. 2 weeks post-op. Brace management and ROM progression."),
    ],
    "WRIST_FRACTURE": [
        ("PHYSICAL THERAPY", "ROUTINE", "Post-operative hand therapy following ORIF right distal radius. Wrist ROM, grip strengthening, scar management. 2x/week x 6 weeks."),
    ],
    "BILATERAL_KNEE_OA": [
        ("PHYSICAL THERAPY", "ROUTINE", "Pre-operative prehab for planned left TKA. Quad strengthening, ROM optimization, gait training. Also post-op rehab anticipated."),
        ("ORTHOPEDICS", "ROUTINE", "Pre-operative evaluation for bilateral TKA. Left side planned first. Right TKA 4-6 months later."),
        ("PAIN MANAGEMENT", "ROUTINE", "Chronic bilateral knee pain refractory to conservative management. Consider joint injections and multimodal approach."),
    ],
    "SPINE_COMPLEX": [
        ("PHYSICAL THERAPY", "ROUTINE", "L4-5 disc herniation with left L5 radiculopathy. McKenzie-based approach, core stabilization, neural mobilization. 2x/week x 6-8 weeks."),
        ("PAIN MANAGEMENT", "ROUTINE", "Lumbar radiculopathy refractory to oral medications. Consider epidural steroid injection at L4-5."),
        ("NEUROLOGY", "ROUTINE", "Left L5 radiculopathy. EMG/NCS recommended to assess severity and prognosis."),
    ],
    "BKA": [
        ("PHYSICAL THERAPY", "ROUTINE", "Pre-prosthetic rehabilitation following left transtibial amputation. Upper body conditioning, transfers, core strengthening, residual limb desensitization. 3x/week."),
        ("PROSTHETICS", "ROUTINE", "Prosthetic limb evaluation and fitting for left BKA. Residual limb healing well. Ready for casting and fabrication."),
        ("PSYCHOLOGY", "ROUTINE", "Adjustment disorder following traumatic amputation. Grief counseling and body image therapy needed."),
    ],
    "CARDIAC_COMPLEX": [
        ("CARDIOLOGY", "ROUTINE", "CHF exacerbation. NYHA Class III, worsening from II. LVEF declining. Volume overloaded. Needs optimization."),
        ("NUTRITION", "ROUTINE", "Cardiac diet counseling. Sodium restriction education. Recent dietary indiscretion contributing to CHF exacerbation."),
    ],
    "STROKE_REHAB": [
        ("PHYSICAL THERAPY", "STAT", "Acute right MCA CVA with left hemiparesis. Inpatient rehabilitation. Gait training, balance, LE strengthening. Daily therapy."),
        ("PSYCHOLOGY", "ROUTINE", "Post-stroke depression screening and management. High risk for adjustment disorder."),
        ("SOCIAL WORK", "ROUTINE", "Discharge planning for stroke patient. Evaluate home safety, caregiver support, and community resources."),
    ],
    "MH_COMPLEX": [
        ("PSYCHOLOGY", "ROUTINE", "PTSD evaluation. CPT therapy initiation. PCL-5 score 62. Severe symptoms with nightmares, hypervigilance, avoidance."),
        ("SOCIAL WORK", "ROUTINE", "Benefits counseling. Substance abuse treatment referral. Housing stability assessment."),
    ],
    "ROTATOR_CUFF": [
        ("PHYSICAL THERAPY", "ROUTINE", "Post-operative left rotator cuff repair rehabilitation. Phase 2 protocol (6-12 weeks post-op). Active-assisted ROM, scapular stabilization. 2x/week x 6 weeks, then Phase 3."),
        ("ORTHOPEDICS", "ROUTINE", "Post-operative follow-up left arthroscopic rotator cuff repair. 6 weeks post-op. Transitioning from sling to active-assisted ROM."),
    ],
    "ACHILLES_RUPTURE": [
        ("PHYSICAL THERAPY", "ROUTINE", "Post-operative right Achilles tendon repair rehabilitation. 6 weeks post-op. Gentle ROM, progressive weight-bearing, calf strengthening program. 2-3x/week x 8 weeks."),
    ],
    "PULMONARY_COMPLEX": [
        ("PULMONARY", "ROUTINE", "COPD management. GOLD Stage III (progression). Increasing exacerbation frequency. PFTs show declining FEV1."),
        ("SLEEP MEDICINE", "ROUTINE", "OSA with CPAP. CPAP compliance review and pressure adjustment."),
    ],
}

with open(os.path.join(OUT_DIR, "consults.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA REQUEST/CONSULTATION file #123 (^GMR(123,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^GMR(123,IEN,0)="ToService^PatientDFN(;ptr)^Urgency^FromService^RequestingProvider"\n')
    f.write('; Reason: ^GMR(123,IEN,20,n,0)="reason text"\n')
    f.write(";\n")
    for p in patients:
        profile = p.get("profile")
        consults_for_patient = []

        # Profile patients get profile-specific consults
        if profile and profile["name"] in PROFILE_CONSULT_REASONS:
            profile_consults = PROFILE_CONSULT_REASONS[profile["name"]]
            consults_for_patient.extend(profile_consults)

        # All patients also have a chance of generic consults
        if not profile or random.random() < 0.4:
            num_generic = random.choices([1, 2], weights=[60, 40])[0]
            services_chosen = random.sample(CONSULT_SERVICES, min(num_generic, len(CONSULT_SERVICES)))
            for svc in services_chosen:
                reasons = random.choice(CONSULT_REASONS.get(svc, [["Evaluate and treat."]]))
                if isinstance(reasons, str):
                    reason_text = reasons
                else:
                    reason_text = reasons
                urgency = random.choices(["ROUTINE","STAT"], weights=[85,15])[0]
                consults_for_patient.append((svc, urgency, reason_text))

        # Skip patients with no consults
        if not consults_for_patient and random.random() < 0.5:
            continue

        for consult_data in consults_for_patient:
            consult_ien += 1
            svc, urgency, reason_text = consult_data
            from_svc = random.choice(["PRIMARY CARE","EMERGENCY","MENTAL HEALTH","SPINE CLINIC","ORTHOPEDIC CLINIC"])
            provider = random.choice(DOCTOR_NAMES)

            f.write(f'^GMR(123,{consult_ien},0)="{svc}^{p["ien"]};DPT(^{urgency}^{from_svc}^{provider}"\n')
            if isinstance(reason_text, list):
                for ri, reason in enumerate(reason_text, 1):
                    f.write(f'^GMR(123,{consult_ien},20,{ri},0)="{reason}"\n')
            else:
                # Split long reason text into multiple lines (~80 chars each)
                words = reason_text.split()
                lines = []
                current = ""
                for word in words:
                    if len(current) + len(word) + 1 > 80:
                        lines.append(current.strip())
                        current = word
                    else:
                        current = current + " " + word if current else word
                if current:
                    lines.append(current.strip())
                for ri, line in enumerate(lines, 1):
                    f.write(f'^GMR(123,{consult_ien},20,{ri},0)="{line}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE surgery.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing surgery.zwr...")
surg_ien = 0
surgery_patients = []
with open(os.path.join(OUT_DIR, "surgery.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA SURGERY file #130 (^SRF) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^SRF(IEN,0)="PatientDFN(;ptr)^Procedure^DateOfOp(FM)^SurgeonDFN^Anesthesia^Specialty^PreOpDiag"\n')
    f.write('; Operative report: ^SRF(IEN,"OP",n,0)="text"\n')
    f.write(";\n")
    for p in patients:
        profile = p.get("profile")
        proc_data = None

        # Profile patients with surgery get their specific procedure
        if profile and profile.get("surgery"):
            surgery_key = profile["surgery"]
            if surgery_key in PROFILE_SURGERY_MAP:
                proc_data = PROFILE_SURGERY_MAP[surgery_key]
        # Non-profile patients: ~15% chance of random surgery (up from ~12%)
        elif random.random() < 0.15:
            proc = random.choice(SURGERY_PROCEDURES)
            proc_data = proc

        if proc_data is None:
            continue

        proc_name, anesthesia, specialty, preop_diag, op_lines = proc_data
        surg_ien += 1
        surgeon_dfn = random.choice(role_ien_map["SURGEON"])
        op_date = random_fm_clinical_date()

        f.write(f'^SRF({surg_ien},0)="{p["ien"]};DPT(^{proc_name}^{op_date}^{surgeon_dfn}^{anesthesia}^{specialty}^{preop_diag}"\n')
        f.write(f'^SRF({surg_ien},"OP",1,0)="OPERATIVE REPORT"\n')
        for li, line in enumerate(op_lines, 2):
            f.write(f'^SRF({surg_ien},"OP",{li},0)="{line}"\n')

        surgery_patients.append((p, op_date, specialty))


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE radiology.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing radiology.zwr...")
rad_ien = 0
with open(os.path.join(OUT_DIR, "radiology.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA RAD/NUC MED ORDERS file #75.1 (^RA(75.1,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^RA(75.1,IEN,0)="PatientDFN(;ptr)^Procedure^ImagingType^Urgency^RequestingProvider^ClinicalHistory"\n')
    f.write('; Report: ^RA(75.1,IEN,"RPT",n,0)="text"\n')
    f.write(";\n")
    for p in patients:
        profile = p.get("profile")
        studies_for_patient = []

        # Profile patients get profile-specific radiology
        if profile and profile["name"] in PROFILE_RAD_MAP:
            for study in PROFILE_RAD_MAP[profile["name"]]:
                studies_for_patient.append(study)

        # All patients also have a chance of generic radiology
        if not profile or random.random() < 0.35:
            num_generic = random.choices([1, 2], weights=[70, 30])[0]
            chosen_rads = random.sample(RAD_PROCEDURES, min(num_generic, len(RAD_PROCEDURES)))
            studies_for_patient.extend(chosen_rads)

        # Skip patients with nothing
        if not studies_for_patient and random.random() < 0.6:
            continue

        for proc_name, imaging_type, rpt_lines in studies_for_patient:
            rad_ien += 1
            urgency = random.choices(["ROUTINE","STAT"], weights=[85,15])[0]
            provider = random.choice(DOCTOR_NAMES)
            history = random.choice([
                "Follow-up evaluation","Post-operative evaluation",
                "New symptoms","Acute injury evaluation",
                "Rule out acute process","Chronic condition monitoring",
                "Pre-operative evaluation","Baseline evaluation",
            ])

            f.write(f'^RA(75.1,{rad_ien},0)="{p["ien"]};DPT(^{proc_name}^{imaging_type}^{urgency}^{provider}^{history}"\n')
            for li, line in enumerate(rpt_lines, 1):
                f.write(f'^RA(75.1,{rad_ien},"RPT",{li},0)="{line}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE adt.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing adt.zwr...")
adt_ien = 0
with open(os.path.join(OUT_DIR, "adt.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA PATIENT MOVEMENT file #405 (^DGPT) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^DGPT(IEN,0)="PatientDFN(;ptr)^TransactionType^MovementDT(FM)^Ward^RoomBed^TreatingSpec^AttendPhys^Diagnosis"\n')
    f.write(";\n")
    for p_data, op_date, specialty in surgery_patients:
        physician = random.choice(DOCTOR_NAMES)
        profile = p_data.get("profile")

        # Determine admission pattern based on surgery type
        if specialty == "CARDIAC SURGERY":
            admit_ward = "ICU"
            discharge_day_offset = random.randint(5, 10)
        elif profile and profile.get("surgery") in ("BKA","HEMIARTHROPLASTY"):
            admit_ward = "SURGERY"
            discharge_day_offset = random.randint(4, 7)
        elif profile and profile.get("surgery") in ("ORIF_TIBIA","ORIF_ANKLE","ACL_RECON","TKA"):
            admit_ward = "SURGERY"
            discharge_day_offset = random.randint(1, 3)
        elif profile and profile.get("surgery") in ("ORIF_WRIST","ACHILLES_REPAIR","ROTATOR_CUFF_REPAIR"):
            admit_ward = "SURGERY"
            discharge_day_offset = random.randint(0, 1)  # Often same-day or 1-night stay
        else:
            admit_ward = random.choice(["ICU","SURGERY"])
            discharge_day_offset = random.randint(1, 4)

        admit_room = random.choice(ADT_ROOMS[admit_ward])
        admit_dt = f"{op_date}.0700"

        # Determine discharge diagnosis based on profile
        if profile:
            discharge_diag = f"S/P {profile.get('surgery', 'PROCEDURE')} - DISCHARGED STABLE"
        else:
            discharge_diag = "DISCHARGED STABLE"

        adt_ien += 1
        f.write(f'^DGPT({adt_ien},0)="{p_data["ien"]};DPT(^ADMISSION^{admit_dt}^{admit_ward}^{admit_room}^{specialty}^{physician}^POST-OPERATIVE CARE"\n')

        if admit_ward == "ICU" and random.random() < 0.8:
            transfer_ward = random.choice(["TELEMETRY","MEDICINE","STEP-DOWN"])
            transfer_room = random.choice(ADT_ROOMS[transfer_ward])
            adt_ien += 1
            f.write(f'^DGPT({adt_ien},0)="{p_data["ien"]};DPT(^TRANSFER^{op_date + 2}.1000^{transfer_ward}^{transfer_room}^{specialty}^{physician}^POST-OPERATIVE RECOVERY"\n')
            discharge_ward = transfer_ward
            discharge_room = transfer_room
        else:
            discharge_ward = admit_ward
            discharge_room = admit_room

        adt_ien += 1
        f.write(f'^DGPT({adt_ien},0)="{p_data["ien"]};DPT(^DISCHARGE^{op_date + discharge_day_offset}.1000^{discharge_ward}^{discharge_room}^{specialty}^{physician}^{discharge_diag}"\n')

    # Also add non-surgical admissions for stroke rehab and cardiac profiles
    for p in patients:
        profile = p.get("profile")
        if not profile:
            continue
        if profile["name"] == "STROKE_REHAB":
            physician = random.choice(DOCTOR_NAMES)
            admit_date = random_fm_clinical_date()
            # ER -> ICU -> Neurology -> Rehab -> Discharge
            adt_ien += 1
            f.write(f'^DGPT({adt_ien},0)="{p["ien"]};DPT(^ADMISSION^{admit_date}.0300^ICU^{random.choice(ADT_ROOMS["ICU"])}^NEUROLOGY^{physician}^ACUTE CEREBROVASCULAR ACCIDENT"\n')
            adt_ien += 1
            f.write(f'^DGPT({adt_ien},0)="{p["ien"]};DPT(^TRANSFER^{admit_date + 2}.1000^NEUROLOGY^{random.choice(ADT_ROOMS["NEUROLOGY"])}^NEUROLOGY^{physician}^CVA STABLE FOR STEP-DOWN"\n')
            adt_ien += 1
            f.write(f'^DGPT({adt_ien},0)="{p["ien"]};DPT(^DISCHARGE^{admit_date + 14}.1000^NEUROLOGY^{random.choice(ADT_ROOMS["NEUROLOGY"])}^NEUROLOGY^{physician}^DISCHARGE TO INPATIENT REHAB"\n')
        elif profile["name"] == "CARDIAC_COMPLEX" and profile.get("surgery") != "CABG":
            # CHF exacerbation admission
            if random.random() < 0.5:
                physician = random.choice(DOCTOR_NAMES)
                admit_date = random_fm_recent_date()
                adt_ien += 1
                f.write(f'^DGPT({adt_ien},0)="{p["ien"]};DPT(^ADMISSION^{admit_date}.1400^TELEMETRY^{random.choice(ADT_ROOMS["TELEMETRY"])}^CARDIOLOGY^{physician}^CONGESTIVE HEART FAILURE EXACERBATION"\n')
                adt_ien += 1
                f.write(f'^DGPT({adt_ien},0)="{p["ien"]};DPT(^DISCHARGE^{admit_date + 4}.1000^TELEMETRY^{random.choice(ADT_ROOMS["TELEMETRY"])}^CARDIOLOGY^{physician}^CHF EXACERBATION - DISCHARGED IMPROVED"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE pharmacy.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing pharmacy.zwr...")
rx_ien = 0
with open(os.path.join(OUT_DIR, "pharmacy.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA PRESCRIPTION file #52 (^PS(52,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Format: ^PS(52,IEN,0)="PatientDFN(;ptr)^Drug^Dosage^Route^Schedule^Sig^DaysSupply^Qty^Refills^ProviderDFN"\n')
    f.write(";\n")
    for p in patients:
        for med in p["meds"]:
            rx_ien += 1
            drug, dose, route, sched, sig, days, qty, refills = med
            f.write(f'^PS(52,{rx_ien},0)="{p["ien"]};DPT(^{drug}^{dose}^{route}^{sched}^{sig}^{days}^{qty}^{refills}^{p["provider_dfn"]}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE immunizations.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing immunizations.zwr...")
imm_ien = 0
imm_patient_count = 0
with open(os.path.join(OUT_DIR, "immunizations.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA V IMMUNIZATION file #9000010.11 (^AUPNVIMM) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: VaccineName^CVXCode^EventDate(FM)^Series^LotNumber^Manufacturer^PatientDFN;DPT(\n')
    f.write('; Node .1: AdminSite^Route^Dose^ProviderDFN;VA(200,\n')
    f.write(";\n")
    for p in patients:
        # 60-80% of patients get immunizations
        if random.random() > 0.70:  # ~70% get immunizations (in the 60-80% range)
            continue
        imm_patient_count += 1
        num_imm = random.randint(1, 3)
        chosen_imm = random.sample(IMMUNIZATION_POOL, min(num_imm, len(IMMUNIZATION_POOL)))
        for vax_name, cvx_code in chosen_imm:
            imm_ien += 1
            event_date = random_fm_recent_date()
            series = random.choice(IMMUNIZATION_SERIES)
            lot = get_lot_number()
            manufacturer = random.choice(IMMUNIZATION_MANUFACTURERS)
            admin_site = random.choice(IMMUNIZATION_SITES)
            route = random.choice(IMMUNIZATION_ROUTES)
            dose = "0.5 ML" if route == "INTRAMUSCULAR" else "0.1 ML"
            provider_ien = random.choice(role_ien_map["NURSE"] + role_ien_map["PHYSICIAN"])

            f.write(f'^AUPNVIMM({imm_ien},0)="{vax_name}^{cvx_code}^{event_date}^{series}^{lot}^{manufacturer}^{p["ien"]};DPT("\n')
            f.write(f'^AUPNVIMM({imm_ien},.1)="{admin_site}^{route}^{dose}^{provider_ien};VA(200,"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE nursing.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing nursing.zwr...")
nurs_ien = 0
nurs_patient_count = 0
with open(os.path.join(OUT_DIR, "nursing.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA Nursing Assessment file (^NURS(210,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: PatientDFN;DPT(^AssessType^DateTime(FM)^NurseDFN;VA(200,\n')
    f.write('; Node 1: LOC^Orientation^BreathSounds^O2Therapy^SpO2\n')
    f.write('; Node 2: HeartRhythm^Edema^Skin^BradenScore^MorseScore\n')
    f.write('; Node 3: Pain^PainLocation^BowelSounds^Appetite^UrineOutput^Foley\n')
    f.write('; Node 4: Anxiety^Mood^Mobility^FallRisk^Notes\n')
    f.write(";\n")
    for p in patients:
        # 20% of patients get a nursing assessment
        if random.random() > 0.20:
            continue
        nurs_patient_count += 1
        nurs_ien += 1

        assess_type = random.choice(["ADMISSION","SHIFT","FOCUSED","DISCHARGE"])
        assess_date = random_fm_recent_date()
        hour = random.randint(6, 22)
        minute = random.choice([0, 15, 30, 45])
        assess_dt = f"{assess_date}.{hour:02d}{minute:02d}"
        nurse_dfn = random.choice(role_ien_map["NURSE"])

        # Node 1: Neurological / Respiratory
        loc = random.choices(["ALERT","DROWSY","LETHARGIC"], weights=[70,20,10])[0]
        orient_options = ["PERSON","PLACE","TIME","SITUATION"]
        num_orient = random.randint(2, 4)
        orientation = ",".join(random.sample(orient_options, num_orient))
        breath_sounds = random.choices(["CLEAR","DIMINISHED","CRACKLES","WHEEZES"], weights=[50,25,15,10])[0]
        o2_therapy = random.choices(["NONE","NASAL CANNULA 2L","NASAL CANNULA 4L","FACE MASK 40%","BIPAP"], weights=[60,20,10,5,5])[0]
        spo2 = random.randint(88, 100)

        # Node 2: Cardiovascular / Integumentary
        heart_rhythm = random.choices(["REGULAR","IRREGULAR","AFIB","TACHYCARDIA"], weights=[60,15,15,10])[0]
        edema = random.choices(["NONE","1+","2+","3+"], weights=[50,25,15,10])[0]
        skin = random.choices(["INTACT","WOUND","RASH"], weights=[70,20,10])[0]
        braden_score = random.randint(10, 23)
        morse_score = random.randint(0, 125)

        # Node 3: GI / GU / Pain
        pain_level = random.randint(0, 10)
        pain_location = random.choice(PAIN_LOCATIONS) if pain_level > 0 else ""
        bowel_sounds = random.choices(["PRESENT","HYPOACTIVE","HYPERACTIVE"], weights=[70,20,10])[0]
        appetite = random.choices(["GOOD","FAIR","POOR"], weights=[40,35,25])[0]
        urine_output = random.randint(20, 100)
        foley = random.choices(["N","Y"], weights=[75,25])[0]

        # Node 4: Psychosocial / Mobility
        anxiety = random.choices(["NONE","MILD","MODERATE","SEVERE"], weights=[40,30,20,10])[0]
        mood = random.choices(["NORMAL","DEPRESSED","ANXIOUS","AGITATED"], weights=[50,20,20,10])[0]
        mobility = random.choices(["AMBULATORY","ASSISTIVE DEVICE","WHEELCHAIR","BEDBOUND"], weights=[40,25,20,15])[0]
        fall_risk = "HIGH" if morse_score >= 45 else "MODERATE" if morse_score >= 25 else "LOW"
        notes = random.choice([
            "Assessment within normal limits.",
            "Patient resting comfortably.",
            "Wound care completed per protocol.",
            "Patient educated on fall precautions.",
            "Pain management plan reviewed with patient.",
            "Skin assessment completed, no new breakdown.",
            "Patient tolerating diet as ordered.",
            "IV site clean, dry, intact. No signs of infiltration.",
        ])

        f.write(f'^NURS(210,{nurs_ien},0)="{p["ien"]};DPT(^{assess_type}^{assess_dt}^{nurse_dfn};VA(200,"\n')
        f.write(f'^NURS(210,{nurs_ien},1)="{loc}^{orientation}^{breath_sounds}^{o2_therapy}^{spo2}"\n')
        f.write(f'^NURS(210,{nurs_ien},2)="{heart_rhythm}^{edema}^{skin}^{braden_score}^{morse_score}"\n')
        f.write(f'^NURS(210,{nurs_ien},3)="{pain_level}^{pain_location}^{bowel_sounds}^{appetite}^{urine_output}^{foley}"\n')
        f.write(f'^NURS(210,{nurs_ien},4)="{anxiety}^{mood}^{mobility}^{fall_risk}^{notes}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE dental.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing dental.zwr...")
den_ien = 0
den_tx_ien = 0
den_patient_count = 0
with open(os.path.join(OUT_DIR, "dental.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA DENTAL file (^DEN(228,...)) — {patient_count}-patient synthetic data\n")
    f.write('; ^DEN(228,IEN,0): PatientDFN;DPT(^Eligibility^PerioStatus^RemainingTeeth^DentistDFN;VA(200,\n')
    f.write('; ^DEN(228,IEN,1): LastExamDate(FM)^LastXRayDate(FM)^LastCleaningDate(FM)\n')
    f.write('; ^DEN(228.1,IEN,0): PatientDFN;DPT(^ProcCode^ProcDesc^ToothNum^Surface^Date(FM)^ProviderDFN;VA(200,^Status\n')
    f.write(";\n")
    for p in patients:
        # 25% of patients get dental data
        if random.random() > 0.25:
            continue
        den_patient_count += 1
        den_ien += 1

        eligibility = random.choices(["ACTIVE","PENDING","EMERGENCY"], weights=[60,25,15])[0]
        perio_status = random.choices(["HEALTHY","MILD","MODERATE","SEVERE"], weights=[40,30,20,10])[0]
        remaining_teeth = random.randint(20, 32)
        dentist_dfn = random.choice(role_ien_map["DENTIST"])

        last_exam = random_fm_recent_date()
        last_xray = random_fm_clinical_date()
        last_cleaning = random_fm_recent_date()

        f.write(f'^DEN(228,{den_ien},0)="{p["ien"]};DPT(^{eligibility}^{perio_status}^{remaining_teeth}^{dentist_dfn};VA(200,"\n')
        f.write(f'^DEN(228,{den_ien},1)="{last_exam}^{last_xray}^{last_cleaning}"\n')

        # 1-3 dental treatments per patient
        num_tx = random.randint(1, 3)
        chosen_procs = random.sample(DENTAL_PROCEDURES, min(num_tx, len(DENTAL_PROCEDURES)))
        for proc_code, proc_desc in chosen_procs:
            den_tx_ien += 1
            tooth_num = random.randint(1, 32) if proc_code not in ("D0120","D0150","D0210","D1110") else 0
            surface = random.choice(DENTAL_SURFACES) if proc_code in ("D2391",) else ""
            tx_date = random_fm_recent_date()
            status = random.choices(["COMPLETED","SCHEDULED","IN PROGRESS"], weights=[70,20,10])[0]
            provider_dfn = random.choice(role_ien_map["DENTIST"])

            f.write(f'^DEN(228.1,{den_tx_ien},0)="{p["ien"]};DPT(^{proc_code}^{proc_desc}^{tooth_num}^{surface}^{tx_date}^{provider_dfn};VA(200,^{status}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE mental_health.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing mental_health.zwr...")
mh_ien = 0
mh_patient_count = 0

def mh_interpretation(instrument, score):
    """Return clinical interpretation string based on instrument and score."""
    if instrument == "PHQ-9":
        if score <= 4: return "MINIMAL"
        if score <= 9: return "MILD"
        if score <= 14: return "MODERATE"
        if score <= 19: return "MODERATELY SEVERE"
        return "SEVERE"
    elif instrument == "GAD-7":
        if score <= 4: return "MINIMAL"
        if score <= 9: return "MILD"
        if score <= 14: return "MODERATE"
        return "SEVERE"
    elif instrument == "PCL-5":
        if score < 31: return "BELOW THRESHOLD"
        if score < 50: return "PROBABLE PTSD"
        return "SEVERE PTSD"
    elif instrument == "AUDIT-C":
        if score <= 3: return "NEGATIVE"
        if score <= 7: return "HAZARDOUS"
        return "HARMFUL/DEPENDENT"
    elif instrument == "COLUMBIA SUICIDE SEVERITY":
        if score == 0: return "NO RISK"
        if score <= 2: return "LOW RISK"
        if score <= 4: return "MODERATE RISK"
        return "HIGH RISK"
    return "UNKNOWN"

def mh_is_positive(instrument, score):
    """Return whether the screening is considered positive."""
    if instrument == "PHQ-9": return "1" if score >= 10 else "0"
    if instrument == "GAD-7": return "1" if score >= 10 else "0"
    if instrument == "PCL-5": return "1" if score >= 31 else "0"
    if instrument == "AUDIT-C": return "1" if score >= 4 else "0"
    if instrument == "COLUMBIA SUICIDE SEVERITY": return "1" if score >= 3 else "0"
    return "0"

with open(os.path.join(OUT_DIR, "mental_health.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA MENTAL HEALTH INSTRUMENT file (^YTT(601,...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: InstrumentName^PatientDFN;DPT(^Date(FM)^TotalScore^Interpretation^IsPositive^ProviderDFN;VA(200,\n')
    f.write(";\n")
    for p in patients:
        # 35% of patients, correlated with MH problems
        has_mh_problem = patient_has_icd10(p, MH_ICD10S)
        threshold = 0.65 if has_mh_problem else 0.20  # Higher chance if MH problems
        if random.random() > threshold:
            continue
        mh_patient_count += 1

        # 1-3 instruments per patient
        num_instruments = random.randint(1, 3)
        chosen = random.sample(MH_INSTRUMENTS, min(num_instruments, len(MH_INSTRUMENTS)))

        # If patient has PTSD, ensure PCL-5 is included
        if has_mh_problem and any(icd == "F43.10" for _, icd, _, _ in p["problems"]):
            if not any(instr == "PCL-5" for instr, _ in chosen):
                chosen.append(("PCL-5", 80))
                chosen = chosen[:3]  # Cap at 3

        for instr_name, max_score in chosen:
            mh_ien += 1
            score = random.randint(0, max_score)
            # Bias scores higher if patient has MH problems
            if has_mh_problem and instr_name in ("PHQ-9","GAD-7","PCL-5"):
                score = random.randint(max_score // 3, max_score)
            interp = mh_interpretation(instr_name, score)
            positive = mh_is_positive(instr_name, score)
            test_date = random_fm_recent_date()
            provider_dfn = random.choice(role_ien_map["MH_PROVIDER"] + role_ien_map["PHYSICIAN"])

            f.write(f'^YTT(601,{mh_ien},0)="{instr_name}^{p["ien"]};DPT(^{test_date}^{score}^{interp}^{positive}^{provider_dfn};VA(200,"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE social_work.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing social_work.zwr...")
sw_ien = 0
sw_patient_count = 0
with open(os.path.join(OUT_DIR, "social_work.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA SOCIAL WORK file (^SW(...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: PatientDFN;DPT(^AssessType^Date(FM)^SWerDFN;VA(200,^RiskLevel\n')
    f.write('; Node 1: Housing^Employment^SocialSupport^SubstanceUse^LegalIssues\n')
    f.write('; Node 2: DischargePlan^DischargeBarriers^Recommendations\n')
    f.write(";\n")
    for p in patients:
        # 20% of patients
        if random.random() > 0.20:
            continue
        sw_patient_count += 1
        sw_ien += 1

        assess_type = random.choice(SW_ASSESS_TYPES)
        assess_date = random_fm_recent_date()
        sw_dfn = random.choice(role_ien_map["SOCIAL_WORKER"])
        risk_level = random.choices(["LOW","MODERATE","HIGH","CRITICAL"], weights=[30,35,25,10])[0]

        housing = random.choices(SW_HOUSING, weights=[50,15,15,10,10])[0]
        employment = random.choices(SW_EMPLOYMENT, weights=[15,25,30,30])[0]
        social_support = random.choice(SW_SUPPORT)
        substance_use = random.choices(SW_SUBSTANCE, weights=[40,20,15,5,5,15])[0]
        legal_issues = random.choices(SW_LEGAL, weights=[60,10,10,10,10])[0]

        discharge_plan = random.choice([
            "HOME WITH HOME HEALTH","HOME WITH FAMILY SUPPORT","SKILLED NURSING FACILITY",
            "DOMICILIARY","COMMUNITY LIVING CENTER","HOME SELF-CARE","HOMELESS SHELTER REFERRAL",
        ]) if assess_type == "DISCHARGE_PLANNING" else ""

        num_barriers = random.randint(0, 2)
        barriers = ";".join(random.sample(SW_DISCHARGE_BARRIERS, num_barriers)) if num_barriers > 0 else ""
        num_recs = random.randint(1, 3)
        recommendations = ";".join(random.sample(SW_RECOMMENDATIONS, num_recs))

        f.write(f'^SW({sw_ien},0)="{p["ien"]};DPT(^{assess_type}^{assess_date}^{sw_dfn};VA(200,^{risk_level}"\n')
        f.write(f'^SW({sw_ien},1)="{housing}^{employment}^{social_support}^{substance_use}^{legal_issues}"\n')
        f.write(f'^SW({sw_ien},2)="{discharge_plan}^{barriers}^{recommendations}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE health_factors.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing health_factors.zwr...")
hf_ien = 0
hf_patient_count = 0
with open(os.path.join(OUT_DIR, "health_factors.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA HEALTH FACTORS file (^AUPNHF(...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: FactorName^Category^Date(FM)^Level^PatientDFN;DPT(^ProviderDFN;VA(200,\n')
    f.write(";\n")
    for p in patients:
        # 50% of patients get 1-3 health factors
        if random.random() > 0.50:
            continue
        hf_patient_count += 1
        num_hf = random.randint(1, 3)
        chosen_hf = random.sample(HEALTH_FACTOR_POOL, min(num_hf, len(HEALTH_FACTOR_POOL)))
        for factor_name, category, level in chosen_hf:
            hf_ien += 1
            factor_date = random_fm_recent_date()
            provider_dfn = random.choice(role_ien_map["PHYSICIAN"] + role_ien_map["NURSE"])

            f.write(f'^AUPNHF({hf_ien},0)="{factor_name}^{category}^{factor_date}^{level}^{p["ien"]};DPT(^{provider_dfn};VA(200,"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE diet_orders.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing diet_orders.zwr...")
diet_ien = 0
diet_patient_count = 0
with open(os.path.join(OUT_DIR, "diet_orders.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA DIETETICS file (^FH(...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: PatientDFN;DPT(^DietType^CurrentDiet^Modifications^Texture^FluidConsist^CalLevel^StartDate(FM)^ProviderDFN;VA(200,\n')
    f.write(";\n")
    for p in patients:
        # 15% of patients, correlated with diabetes/CHF/CKD
        has_diet_condition = patient_has_icd10(p, DIET_ICD10S)
        threshold = 0.40 if has_diet_condition else 0.10
        if random.random() > threshold:
            continue
        diet_patient_count += 1
        diet_ien += 1

        # Pick diet correlated with condition
        if has_diet_condition:
            patient_icd10s = {icd for _, icd, _, _ in p["problems"]}
            if "E11.9" in patient_icd10s:
                diet = DIET_POOL[2]  # DIABETIC
            elif "I50.9" in patient_icd10s:
                diet = DIET_POOL[1]  # CARDIAC
            elif "N18.3" in patient_icd10s:
                diet = DIET_POOL[3]  # RENAL
            else:
                diet = random.choice(DIET_POOL)
        else:
            diet = random.choice(DIET_POOL)

        diet_type, current_diet, mods, texture, fluid_consist, cal_level = diet
        start_date = random_fm_recent_date()
        provider_dfn = random.choice(role_ien_map["PHYSICIAN"])

        f.write(f'^FH({diet_ien},0)="{p["ien"]};DPT(^{diet_type}^{current_diet}^{mods}^{texture}^{fluid_consist}^{cal_level}^{start_date}^{provider_dfn};VA(200,"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE prosthetics.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing prosthetics.zwr...")
pros_ien = 0
pros_patient_count = 0
with open(os.path.join(OUT_DIR, "prosthetics.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA PROSTHETICS file (^RMPR(...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: PatientDFN;DPT(^Item^HCPCSCode^Category^DateIssued(FM)^Qty^Cost^ProviderDFN;VA(200,^SC\n')
    f.write(";\n")
    for p in patients:
        # 15% of patients, correlated with SC conditions
        is_sc = p["sc_pct"] > 0
        threshold = 0.30 if is_sc else 0.08
        if random.random() > threshold:
            continue
        pros_patient_count += 1

        num_items = random.randint(1, 2)
        chosen_items = random.sample(PROSTHETICS_POOL, min(num_items, len(PROSTHETICS_POOL)))
        for item_name, hcpcs, category in chosen_items:
            pros_ien += 1
            date_issued = random_fm_clinical_date()
            qty = random.randint(1, 2)
            cost = round(random.uniform(50, 5000), 2)
            provider_dfn = random.choice(role_ien_map["PHYSICIAN"])
            sc_flag = "Y" if is_sc and random.random() < 0.7 else "N"

            f.write(f'^RMPR({pros_ien},0)="{p["ien"]};DPT(^{item_name}^{hcpcs}^{category}^{date_issued}^{qty}^{cost}^{provider_dfn};VA(200,^{sc_flag}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE means_test.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing means_test.zwr...")
mt_ien = 0
mt_patient_count = 0
with open(os.path.join(OUT_DIR, "means_test.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA MEANS TEST file (^DGMT(...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: PatientDFN;DPT(^TestType^Date(FM)^Income^NetWorth^Dependents^EligStatus^PriorityGroup^ClerkDFN;VA(200,\n')
    f.write(";\n")
    for p in patients:
        # 60% of patients get means test
        if random.random() > 0.60:
            continue
        mt_patient_count += 1
        mt_ien += 1

        test_type = random.choices(["MEANS TEST","COPAY TEST","HARDSHIP"], weights=[70,20,10])[0]
        test_date = random_fm_clinical_date()
        income = random.randint(0, 120000)
        net_worth = random.randint(0, 500000)
        dependents = random.randint(0, 6)

        # Eligibility status based on income and SC
        if p["sc_pct"] >= 50:
            elig_status = "SC 50% OR GREATER"
            priority = random.choice(["1","2","3"])
        elif p["sc_pct"] > 0:
            elig_status = "SC LESS THAN 50%"
            priority = random.choice(["3","4","5"])
        elif income < 30000:
            elig_status = "BELOW THRESHOLD"
            priority = random.choice(["5","6"])
        elif income < 80000:
            elig_status = "ABOVE THRESHOLD"
            priority = random.choice(["7","8"])
        else:
            elig_status = "ABOVE THRESHOLD"
            priority = "8"

        clerk_dfn = random.choice(role_ien_map["REG_CLERK"])

        f.write(f'^DGMT({mt_ien},0)="{p["ien"]};DPT(^{test_type}^{test_date}^{income}^{net_worth}^{dependents}^{elig_status}^{priority}^{clerk_dfn};VA(200,"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE sc_conditions.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing sc_conditions.zwr...")
sc_ien = 0
sc_patient_count = 0
with open(os.path.join(OUT_DIR, "sc_conditions.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA SC CONDITIONS file (^DGS(...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: PatientDFN;DPT(^Condition^ICD10^Percentage^IsServiceConnected^EffectiveDate(FM)^Extremity\n')
    f.write(";\n")
    for p in patients:
        # Only for SC patients (SC% > 0)
        if p["sc_pct"] == 0:
            continue

        sc_patient_count += 1
        remaining_pct = p["sc_pct"]
        sc_problems = [(cond, icd, sc_prob) for cond, icd, _, sc_prob in p["problems"]]
        # Shuffle to vary which conditions are SC
        random.shuffle(sc_problems)

        for cond, icd, sc_prob in sc_problems:
            if remaining_pct <= 0:
                break
            if random.random() > sc_prob:
                continue

            sc_ien += 1
            # Assign a percentage (10-50% each, not exceeding remaining)
            individual_pct = min(random.choice([10, 20, 30, 40, 50]), remaining_pct)
            remaining_pct -= individual_pct
            eff_date = random_fm_onset()

            # Extremity for musculoskeletal conditions
            extremity = ""
            if "KNEE" in cond or "HIP" in cond:
                extremity = random.choice(["LEFT","RIGHT","BILATERAL"])
            elif "BACK" in cond or "LUMBAR" in cond or "CERVICAL" in cond:
                extremity = "SPINE"
            elif "NEUROPATHY" in cond:
                extremity = random.choice(["UPPER","LOWER","BILATERAL LOWER"])

            f.write(f'^DGS({sc_ien},0)="{p["ien"]};DPT(^{cond}^{icd}^{individual_pct}^Y^{eff_date}^{extremity}"\n')


# ══════════════════════════════════════════════════════════════════════════════
#  WRITE reminders.zwr
# ══════════════════════════════════════════════════════════════════════════════

print("Writing reminders.zwr...")
rem_ien = 0
rem_patient_count = 0
with open(os.path.join(OUT_DIR, "reminders.zwr"), "w", newline="\n") as f:
    f.write(f"; VistA CLINICAL REMINDERS file (^PXRMPT(...)) — {patient_count}-patient synthetic data\n")
    f.write('; Node 0: ReminderName^Category^Priority^Frequency^DueDate(FM)^PatientDFN;DPT(\n')
    f.write(";\n")
    for p in patients:
        # 40% of patients get reminders
        if random.random() > 0.40:
            continue
        rem_patient_count += 1

        num_reminders = random.randint(1, 4)
        chosen_reminders = random.sample(REMINDER_POOL, min(num_reminders, len(REMINDER_POOL)))
        for reminder_name, category, priority, frequency in chosen_reminders:
            rem_ien += 1
            due_date = random_fm_future_date()

            f.write(f'^PXRMPT({rem_ien},0)="{reminder_name}^{category}^{priority}^{frequency}^{due_date}^{p["ien"]};DPT("\n')


# ══════════════════════════════════════════════════════════════════════════════
#  SUMMARY
# ══════════════════════════════════════════════════════════════════════════════

profile_count = sum(1 for p in patients if p.get("profile"))
profile_names = set(p["profile"]["name"] for p in patients if p.get("profile"))
pt_consult_count = sum(1 for p in patients if p.get("profile") and p["profile"].get("pt_consult"))

print(f"\n{'='*60}")
print(f"  Generation Complete — {patient_count} patients")
print(f"{'='*60}")
print(f"  Users:             70 (14 roles x 5)")
print(f"  Patients:          {patient_count}")
print(f"    Clinical Profiles: {profile_count} patients ({len(profile_names)} unique profiles)")
print(f"    PT Pathways:       {pt_consult_count} patients with PT consults")
print(f"  Care Team:         {care_team_count} assignments (all {patient_count} patients covered)")
print(f"  Allergies:         {allergy_ien} records")
print(f"  Problems:          {prob_ien} records")
print(f"  Orders:            {order_ien} records")
print(f"  Labs:              (condition-correlated, multi-set per patient)")
print(f"  Vitals:            {vital_ien} measurements (2-4 sets per patient)")
print(f"  TIU Notes:         {tiu_ien} documents (2-5 per patient, SOAP format)")
print(f"  Consults:          {consult_ien} referrals (profile-specific + generic)")
print(f"  Surgeries:         {surg_ien} cases (profile-linked fracture/ortho/cardiac)")
print(f"  Radiology:         {rad_ien} studies (profile-linked + generic)")
print(f"  ADT:               {adt_ien} movements")
print(f"  Prescriptions:     {rx_ien} Rx records")
print(f"  Immunizations:     {imm_ien} records ({imm_patient_count} patients)")
print(f"  Nursing:           {nurs_ien} assessments ({nurs_patient_count} patients)")
print(f"  Dental:            {den_ien} patients, {den_tx_ien} treatments")
print(f"  Mental Health:     {mh_ien} screenings ({mh_patient_count} patients)")
print(f"  Social Work:       {sw_ien} assessments ({sw_patient_count} patients)")
print(f"  Health Factors:    {hf_ien} records ({hf_patient_count} patients)")
print(f"  Diet Orders:       {diet_ien} orders ({diet_patient_count} patients)")
print(f"  Prosthetics:       {pros_ien} items ({pros_patient_count} patients)")
print(f"  Means Tests:       {mt_ien} records ({mt_patient_count} patients)")
print(f"  SC Conditions:     {sc_ien} records ({sc_patient_count} patients)")
print(f"  Reminders:         {rem_ien} reminders ({rem_patient_count} patients)")
print(f"{'='*60}")
print(f"  Output directory:  {OUT_DIR}")
print(f"  Files generated:   {24} ZWR files")
print(f"{'='*60}")
